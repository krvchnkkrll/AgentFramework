using System.Collections.Concurrent;
using Assistant.Contracts.Models;
using Assistant.Documents;
using Assistant.Options;
using Assistant.Prompts;
using Assistant.Search;
using Assistant.Tools;
using FileService.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assistant.Agents;

/// <summary>
/// Собирает агентов и держит их в памяти.
///
/// Зачем кэш: на каждый прогон заново поднимать полдюжины провайдеров — дорого и бессмысленно, настройки агента меняются раз в день, а сообщения
/// идут постоянно. Ключ кэша — идентификатор агента, а признак устаревания — метка UpdatedAt
/// из БД: поправил пользователь агента в конструкторе — рантайм пересобирается на следующем
/// сообщении, без перезапуска приложения.
///
/// Сессии здесь не хранятся: рантайм без состояния, всё состояние разговора лежит в AgentSession,
/// которую приносит с собой каждый запрос.
/// </summary>
internal sealed class AgentRuntimeFactory : IDisposable
{
    /// <summary>Ключ встроенного агента — у него нет идентификатора в БД.</summary>
    private static readonly Guid BuiltInAgentKey = Guid.Empty;

    private readonly ConcurrentDictionary<Guid, AgentRuntime> _runtimes = new();

    private readonly IChatClient _chatClient;
    private readonly AssistantOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentRuntimeFactory> _logger;
    private readonly OpenSearchTextSearchClient? _searchClient;
    private readonly IFileService? _fileService;
    private readonly InMemoryDocumentStore? _documentStore;

    public AgentRuntimeFactory(
        IChatClient chatClient,
        IOptions<AssistantOptions> options,
        ILoggerFactory loggerFactory,
        OpenSearchTextSearchClient? searchClient = null,
        IFileService? fileService = null,
        InMemoryDocumentStore? documentStore = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _chatClient = chatClient;
        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AgentRuntimeFactory>();
        _searchClient = searchClient;
        _fileService = fileService;
        _documentStore = documentStore;
    }

    public AssistantOptions Options => _options;

    /// <summary>
    /// Возвращает готовый агент. Для <paramref name="definition"/> = null — встроенный,
    /// собранный целиком из appsettings.
    ///
    /// Метод синхронный: для сборки агенту нужны только имена и описания скиллов, а они уже
    /// пришли из базы в <paramref name="definition"/>. Тексты скиллов качаются из файлового
    /// сервиса позже — во время прогона, когда модель вызовет load_skill.
    /// </summary>
    public AgentRuntime Get(AssistantAgentDefinition? definition)
    {
        var signature = BuildSkillsSignature(definition);

        return TryGetCached(definition, signature, out var cached)
            ? cached
            : Store(definition, signature);
    }

    private bool TryGetCached(AssistantAgentDefinition? definition, string signature, out AgentRuntime runtime)
    {
        var key = definition?.Id ?? BuiltInAgentKey;
        var version = definition?.UpdatedAt ?? DateTimeOffset.MinValue;

        if (_runtimes.TryGetValue(key, out var existing)
            && existing.Version >= version
            && string.Equals(existing.SkillsSignature, signature, StringComparison.Ordinal))
        {
            runtime = existing;
            return true;
        }

        runtime = null!;
        return false;
    }

    private AgentRuntime Store(AssistantAgentDefinition? definition, string signature)
    {
        var key = definition?.Id ?? BuiltInAgentKey;
        var version = definition?.UpdatedAt ?? DateTimeOffset.MinValue;

        _runtimes.TryGetValue(key, out var existing);

        var rebuilt = Build(definition, signature);

        // Гонку двух параллельных сборок разруливаем просто: побеждает последняя записанная,
        // проигравшую освобождаем. Обе рабочие, так что кому именно достанется этот запрос — неважно.
        var stored = _runtimes.AddOrUpdate(
            key,
            rebuilt,
            (_, current) =>
                current.Version >= version
                && string.Equals(current.SkillsSignature, signature, StringComparison.Ordinal)
                    ? current
                    : rebuilt);

        if (!ReferenceEquals(stored, rebuilt))
        {
            rebuilt.Dispose();
            return stored;
        }

        if (existing is not null && !ReferenceEquals(existing, stored))
            existing.Dispose();

        return stored;
    }

    /// <summary>
    /// Идентификаторы скиллов вместе с хэшами их содержимого, в устойчивом порядке.
    /// Строка меняется и когда скиллы переставили в конструкторе, и когда перезалили
    /// содержимое любого из них.
    /// </summary>
    private static string BuildSkillsSignature(AssistantAgentDefinition? definition)
    {
        if (definition is null || definition.Skills.Count == 0)
            return string.Empty;

        return string.Join(
            '|',
            definition.Skills
                .OrderBy(skill => skill.Id)
                .Select(skill => $"{skill.Id:N}:{skill.ContentHash}"));
    }

    /// <summary>Выбрасывает агента из кэша — например, когда его удалили в конструкторе.</summary>
    public void Evict(Guid agentId)
    {
        if (_runtimes.TryRemove(agentId, out var runtime))
            runtime.Dispose();
    }

    private AgentRuntime Build(AssistantAgentDefinition? definition, string skillsSignature)
    {
        var tools = CreateTools();
        var providers = AgentContextProviderFactory.Create(
            _options,
            definition,
            _chatClient,
            _searchClient,
            _fileService,
            _documentStore,
            _loggerFactory);

        var chatAgent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Id = definition?.Id.ToString() ?? "default-agent",
                Name = definition?.Name ?? "DefaultAgent",
                Description = definition?.Description
                    ?? "Универсальный ассистент общего назначения с доступом к набору служебных инструментов.",
                ChatOptions = BuildChatOptions(definition, tools),

                // История живёт в памяти сессии, а сессия целиком сериализуется в БД.
                // Обрезкой занимается не этот провайдер, а CompactionProvider — он умнее редьюсера.
                ChatHistoryProvider = new InMemoryChatHistoryProvider(),

                AIContextProviders = providers.Providers,
            },
            _loggerFactory);

        var agent = BuildPipeline(chatAgent, definition);

        _logger.LogInformation(
            "Агент {AgentName} собран на модели {Model}. Инструменты: {Tools}. Провайдеры: {Providers}.",
            definition?.Name ?? "встроенный",
            _options.Model,
            tools.Count == 0 ? "нет" : string.Join(", ", tools.Select(tool => tool.Name)),
            providers.Descriptions.Count == 0 ? "нет" : string.Join("; ", providers.Descriptions));

        return new AgentRuntime(
            agent,
            providers,
            [.. tools.Select(tool => tool.Name)],
            definition?.UpdatedAt ?? DateTimeOffset.MinValue,
            skillsSignature);
    }

    private ChatOptions BuildChatOptions(AssistantAgentDefinition? definition, IReadOnlyList<AITool> tools)
    {
        var generation = _options.Generation;

        return new ChatOptions
        {
            ModelId = _options.Model,
            Instructions = definition?.Instructions
                ?? generation.SystemPrompt
                ?? DefaultPrompts.System,
            Temperature = definition?.Temperature ?? generation.Temperature,
            TopP = definition?.TopP ?? generation.TopP,
            TopK = definition?.TopK ?? generation.TopK,
            MaxOutputTokens = definition?.MaxOutputTokens ?? generation.MaxOutputTokens,
            FrequencyPenalty = definition?.FrequencyPenalty ?? generation.FrequencyPenalty,
            PresencePenalty = definition?.PresencePenalty ?? generation.PresencePenalty,
            Reasoning = BuildReasoning(definition is null
                ? generation.ReasoningEffort
                : Map(definition.ReasoningEffortEnum)),
            ToolMode = ChatToolMode.Auto,
            AllowMultipleToolCalls = generation.AllowMultipleToolCalls,
            Tools = [.. tools],
        };
    }

    private static ReasoningEffortOption Map(AssistantReasoningEffortEnum effort) => effort switch
    {
        AssistantReasoningEffortEnum.Default => ReasoningEffortOption.Default,
        AssistantReasoningEffortEnum.None => ReasoningEffortOption.None,
        AssistantReasoningEffortEnum.Low => ReasoningEffortOption.Low,
        AssistantReasoningEffortEnum.Medium => ReasoningEffortOption.Medium,
        AssistantReasoningEffortEnum.High => ReasoningEffortOption.High,
        _ => ReasoningEffortOption.Default,
    };

    /// <summary>
    /// Выключатель «мыслей» модели: в теле запроса уезжает reasoning_effort. Именно генерация
    /// рассуждений съедает основное время ответа (сотни токенов до первого символа текста).
    /// Понимает не всякий сервер и не всякая модель: если параметр проигнорируют, модель
    /// просто продолжит думать.
    /// </summary>
    internal static ReasoningOptions? BuildReasoning(ReasoningEffortOption effort) => effort switch
    {
        ReasoningEffortOption.Default => null,
        ReasoningEffortOption.None => new ReasoningOptions { Effort = ReasoningEffort.None },
        ReasoningEffortOption.Low => new ReasoningOptions { Effort = ReasoningEffort.Low },
        ReasoningEffortOption.Medium => new ReasoningOptions { Effort = ReasoningEffort.Medium },
        ReasoningEffortOption.High => new ReasoningOptions { Effort = ReasoningEffort.High },
        _ => null,
    };

    /// <summary>
    /// Инструменты из списка Approvals:RequireApprovalFor оборачиваются так, что перед вызовом
    /// агент останавливается и просит подтверждения. Остальные вызываются как обычно.
    /// </summary>
    private IReadOnlyList<AITool> CreateTools()
    {
        var requireApproval = _options.Approvals.Enabled
            ? new HashSet<string>(_options.Approvals.RequireApprovalFor, StringComparer.OrdinalIgnoreCase)
            : [];

        return
        [
            .. BuiltInTools.Create(TimeSpan.FromSeconds(_options.Tools.SimulatedDelaySeconds))
                .Select(AITool (tool) => requireApproval.Contains(tool.Name)
                ? new ApprovalRequiredAIFunction(tool)
                : tool),
        ];
    }

    private AIAgent BuildPipeline(AIAgent agent, AssistantAgentDefinition? definition)
    {
        var builder = agent.AsBuilder();

        if (_options.Approvals.Enabled)
        {
            // Реализует «больше не спрашивать» и выстраивает несколько запросов на подтверждение
            // в очередь, чтобы показывать их пользователю по одному.
            builder = builder.UseToolApproval(new ToolApprovalAgentOptions
            {
                AutoApprovalRules = _options.Approvals.AutoApproveReadOnly
                    ?
                    [
                        FileAccessProvider.ReadOnlyToolsAutoApprovalRule,
                        AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule,
                    ]
                    : [],
            });
        }

        builder = builder.Use(LogToolInvocationAsync);
        builder = builder.UseLogging(_loggerFactory);

        if (_options.EnableOpenTelemetry)
            builder = builder.UseOpenTelemetry(definition?.Name ?? "DefaultAgent");

        return builder.Build();
    }

    /// <summary>Логирует каждый вызов инструмента: имя, факт успеха и ошибку, если упал.</summary>
    private async ValueTask<object?> LogToolInvocationAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var name = context.Function.Name;

        _logger.LogInformation("Агент {AgentName} вызывает инструмент {ToolName}.", agent.Name, name);

        try
        {
            var result = await next(context, cancellationToken);

            _logger.LogInformation("Инструмент {ToolName} отработал.", name);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Инструмент {ToolName} упал с ошибкой.", name);
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var runtime in _runtimes.Values)
            runtime.Dispose();

        _runtimes.Clear();
    }
}
