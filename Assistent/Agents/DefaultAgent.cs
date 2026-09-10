using System.Runtime.CompilerServices;
using System.Text.Json;
using Assistant.Contracts;
using Assistant.Contracts.Models;
using Assistant.Documents;
using Assistant.Options;
using Assistant.Prompts;
using Assistant.Search;
using FileService.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assistant.Agents;

/// <summary>
/// Точка входа в ассистента для всего приложения. Сам разговор с моделью ведут агенты,
/// собранные <see cref="AgentRuntimeFactory"/>: встроенный (из appsettings) и пользовательские
/// (из конструктора). Этот класс выбирает нужный, готовит сессию и переводит события
/// фреймворка в события приложения.
///
/// Устройство собранного агента — три слоя:
/// 1. <see cref="ChatClientAgent"/> — системный промпт, параметры генерации, инструменты, история;
/// 2. провайдеры контекста (<see cref="AgentContextProviderFactory"/>) — сжатие истории, скиллы,
///    todo, файлы, память, RAG;
/// 3. middleware поверх агента — подтверждения инструментов, логирование, телеметрия.
/// </summary>
public sealed class DefaultAgent : IAssistantAgent, IDisposable
{
    private static readonly JsonSerializerOptions ToolArgumentsJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentRuntimeFactory _factory;
    private readonly AIAgent _titleAgent;
    private readonly ILogger<DefaultAgent> _logger;
    private readonly AssistantOptions _options;

    public DefaultAgent(
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

        _options = options.Value;
        _logger = loggerFactory.CreateLogger<DefaultAgent>();
        _factory = new AgentRuntimeFactory(
            chatClient, options, loggerFactory, searchClient, fileService, documentStore);

        _titleAgent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = "default-agent-title",
                Name = "DefaultAgentTitle",
                Description = "Служебный агент: придумывает название чата по первому сообщению.",
                ChatOptions = new ChatOptions
                {
                    ModelId = _options.Model,
                    Instructions = _options.Title.Prompt ?? DefaultPrompts.Title,
                    Temperature = _options.Title.Temperature,
                    MaxOutputTokens = _options.Title.MaxOutputTokens,
                    ToolMode = ChatToolMode.None,
                    Reasoning = AgentRuntimeFactory.BuildReasoning(ReasoningEffortOption.None),
                },
            },
            loggerFactory);
    }

    /// <summary>
    /// Голый <see cref="AIAgent"/> встроенного агента — на случай, если понадобится что-то,
    /// чего нет в обёртке (обернуть в LoopAgent, воткнуть в воркфлоу).
    /// </summary>
    public AIAgent Agent => _factory.Get(null).Agent;

    /// <summary>Имена собственных инструментов агента. Инструменты провайдеров сюда не входят.</summary>
    public IReadOnlyList<string> ToolNames => _factory.Get(null).ToolNames;

    /// <summary>Выбрасывает агента из кэша — вызывается, когда его удалили или переписали.</summary>
    public void EvictAgent(Guid agentId) => _factory.Evict(agentId);

    // ---------------------------------------------------------------------
    // Сессии
    // ---------------------------------------------------------------------

    /// <summary>
    /// Восстанавливает сессию из сохранённого состояния, а если его нет — собирает новую
    /// из истории переписки. Разница принципиальная: в состоянии лежит уже сжатая история
    /// плюс todo-лист, режим и подтверждения, а из БД приезжает сырая переписка целиком.
    /// </summary>
    private async Task<AgentSession> CreateSessionAsync(
        AIAgent agent,
        AssistantRunRequest request,
        CancellationToken cancellationToken)
    {
        var session = await RestoreSessionAsync(agent, request.SessionState, cancellationToken)
            ?? await CreateFromHistoryAsync(agent, request.History, cancellationToken);

        // StateBag хранит значения через JSON-сериализацию и типизирован по ссылочным типам,
        // поэтому идентификаторы кладём строками.
        if (request.UserId is { } userId)
            session.StateBag.SetValue(AssistantSessionKeys.UserId, userId.ToString());

        if (request.ConversationId is { } conversationId)
            session.StateBag.SetValue(AssistantSessionKeys.ConversationId, conversationId.ToString());

        return session;
    }

    private async Task<AgentSession?> RestoreSessionAsync(
        AIAgent agent,
        string? state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;

        try
        {
            using var document = JsonDocument.Parse(state);

            return await agent.DeserializeSessionAsync(
                document.RootElement.Clone(),
                jsonSerializerOptions: null,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Ловим всё подряд намеренно. Состояние могло быть записано другой версией фреймворка,
            // другим набором провайдеров или испортиться при хранении — набор исключений, которые
            // при этом прилетят, не описан нигде. Ронять из-за этого весь чат нельзя: история
            // переписки в БД цела, соберём сессию из неё, потеряв только сжатие и todo-лист.
            _logger.LogWarning(exception, "Не удалось восстановить состояние сессии, собираем её из истории чата.");
            return null;
        }
    }

    private static async Task<AgentSession> CreateFromHistoryAsync(
        AIAgent agent,
        IReadOnlyCollection<AssistantMessage> history,
        CancellationToken cancellationToken)
    {
        var session = await agent.CreateSessionAsync(cancellationToken);

        var messages = history
            .Where(message => !string.IsNullOrWhiteSpace(message.Text))
            .Select(ToChatMessage)
            .ToList();

        if (messages.Count > 0)
            session.SetInMemoryChatHistory(messages);

        return session;
    }

    /// <summary>Сериализует сессию — её кладут в БД и возвращают в следующем запросе.</summary>
    private async Task<string?> SerializeSessionAsync(
        AIAgent agent,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await agent.SerializeSessionAsync(session, jsonSerializerOptions: null, cancellationToken);
            return state.GetRawText();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            _logger.LogWarning(exception, "Не удалось сохранить состояние сессии агента.");
            return null;
        }
    }

    // ---------------------------------------------------------------------
    // Запуск
    // ---------------------------------------------------------------------

    /// <summary>
    /// Стриминговый запуск. Отдаёт события по мере генерации: куски текста, рассуждения,
    /// вызовы инструментов и их результаты, запросы на подтверждение и статистику по токенам.
    /// Последним событием всегда идёт состояние сессии.
    /// </summary>
    public async IAsyncEnumerable<AssistantStreamUpdate> RunStreamingAsync(
        AssistantRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserText);

        var runtime = _factory.Get(request.Agent);
        var agent = runtime.Agent;
        var session = await CreateSessionAsync(agent, request, cancellationToken);

        var updates = agent.RunStreamingAsync(request.UserText, session, options: null, cancellationToken);

        // Перебираем стрим руками, а не через await foreach: нам нужно поймать отмену, доотдать
        // наружу состояние сессии и только потом пробросить её дальше. Внутри try с yield нельзя,
        // поэтому цикл разложен на MoveNextAsync + отдельный проход по содержимому.
        await using var enumerator = updates.GetAsyncEnumerator(cancellationToken);

        var cancelled = false;

        while (true)
        {
            AgentResponseUpdate update;

            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;

                update = enumerator.Current;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }

            foreach (var content in update.Contents)
            {
                var mapped = MapContent(content);

                if (mapped is not null)
                    yield return mapped;
            }
        }

        // Состояние сохраняем даже если генерацию отменили: в нём уже лежит то, что успело
        // накопиться, и терять это из-за нажатия «стоп» незачем.
        var state = await SerializeSessionAsync(agent, session, CancellationToken.None);

        if (state is not null)
            yield return AssistantStreamUpdate.ForSessionState(state);

        // Отмену обязательно пробрасываем: вызывающий код по ней отличает «пользователь нажал стоп»
        // от «модель вернула пустой ответ».
        if (cancelled)
            cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Разовый запуск без стриминга: ждём полный ответ целиком. Состояние сессии возвращается
    /// вместе с ответом — сохранять его так же обязательно, как и в стриминге.
    /// </summary>
    public async Task<AssistantReply> RunAsync(
        AssistantRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserText);

        var runtime = _factory.Get(request.Agent);
        var agent = runtime.Agent;
        var session = await CreateSessionAsync(agent, request, cancellationToken);
        var response = await agent.RunAsync(request.UserText, session, options: null, cancellationToken);

        var toolCalls = new Dictionary<string, AssistantToolCall>(StringComparer.Ordinal);

        foreach (var content in response.Messages.SelectMany(message => message.Contents))
            CollectToolCall(content, toolCalls);

        return new AssistantReply
        {
            Text = response.Text,
            ToolCalls = [.. toolCalls.Values],
            Usage = ToUsage(response.Usage),
            FinishReason = response.FinishReason?.Value,
            ResponseId = response.ResponseId,
            SessionState = await SerializeSessionAsync(agent, session, CancellationToken.None),
        };
    }

    /// <summary>
    /// Просит модель придумать название чата по первому сообщению пользователя.
    /// Если модель ответила мусором — возвращает обрезанный текст самого сообщения.
    /// </summary>
    public async Task<string> GenerateTitleAsync(string userText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);

        try
        {
            var response = await _titleAgent.RunAsync(userText, session: null, options: null, cancellationToken);
            var title = NormalizeTitle(response.Text);

            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось сгенерировать название чата, берём текст сообщения.");
        }

        return NormalizeTitle(userText) is { Length: > 0 } fallback ? fallback : "Новый чат";
    }

    // ---------------------------------------------------------------------
    // Маппинг
    // ---------------------------------------------------------------------

    private static AssistantStreamUpdate? MapContent(AIContent content) => content switch
    {
        TextContent { Text.Length: > 0 } text => AssistantStreamUpdate.ForText(text.Text),
        TextReasoningContent { Text.Length: > 0 } reasoning => AssistantStreamUpdate.ForReasoning(reasoning.Text),
        FunctionCallContent call => AssistantStreamUpdate.ForToolCall(
            call.CallId,
            call.Name,
            SerializeArguments(call.Arguments)),
        FunctionResultContent result => AssistantStreamUpdate.ForToolResult(
            result.CallId,
            result.Result?.ToString(),
            result.Exception?.Message),
        ToolApprovalRequestContent { ToolCall: FunctionCallContent call } approval =>
            AssistantStreamUpdate.ForApprovalRequired(
                approval.RequestId,
                call.Name,
                SerializeArguments(call.Arguments)),
        UsageContent usage => AssistantStreamUpdate.ForUsage(ToUsage(usage.Details)!),
        ErrorContent error => AssistantStreamUpdate.ForError(error.Message ?? "Неизвестная ошибка."),
        _ => null,
    };

    private static void CollectToolCall(AIContent content, Dictionary<string, AssistantToolCall> toolCalls)
    {
        switch (content)
        {
            case FunctionCallContent call:
                toolCalls[call.CallId] = new AssistantToolCall
                {
                    CallId = call.CallId,
                    Name = call.Name,
                    Arguments = SerializeArguments(call.Arguments),
                };
                break;

            case FunctionResultContent result:
                if (toolCalls.TryGetValue(result.CallId, out var existing))
                {
                    toolCalls[result.CallId] = existing with
                    {
                        Result = result.Result?.ToString(),
                        Error = result.Exception?.Message,
                    };
                }

                break;
        }
    }

    private static ChatMessage ToChatMessage(AssistantMessage message) => new(
        message.RoleEnum switch
        {
            AssistantRoleEnum.System => ChatRole.System,
            AssistantRoleEnum.User => ChatRole.User,
            AssistantRoleEnum.Assistant => ChatRole.Assistant,
            AssistantRoleEnum.Tool => ChatRole.Tool,
            _ => ChatRole.User,
        },
        message.Text);

    private static AssistantUsage? ToUsage(UsageDetails? usage) => usage is null
        ? null
        : new AssistantUsage
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount,
        };

    private static string? SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return null;

        try
        {
            return JsonSerializer.Serialize(arguments, ToolArgumentsJsonOptions);
        }
        catch (NotSupportedException)
        {
            return string.Join(", ", arguments.Select(pair => $"{pair.Key}={pair.Value}"));
        }
    }

    private string NormalizeTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var title = raw.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? string.Empty;

        title = title.Trim('"', '\'', '«', '»', '`', '*', '#', ' ', '.');

        var maxLength = _options.Title.MaxLength;

        if (title.Length > maxLength)
            title = string.Concat(title.AsSpan(0, maxLength).TrimEnd(), "…");

        return title;
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
