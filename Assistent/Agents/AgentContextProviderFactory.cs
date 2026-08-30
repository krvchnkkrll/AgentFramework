using Assistant.Compaction;
using Assistant.Contracts.Models;
using Assistant.Documents;
using Assistant.Options;
using Assistant.Search;
using Assistant.Skills;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Assistant.Agents;

/// <summary>Собранный набор провайдеров плюс описание для лога.</summary>
internal sealed record AgentProviderSet
{
    public required IReadOnlyList<AIContextProvider> Providers { get; init; }

    public required IReadOnlyList<string> Descriptions { get; init; }
}

/// <summary>
/// Собирает список <see cref="AIContextProvider"/> для агента.
///
/// AIContextProvider — главная точка расширения фреймворка. Провайдер вызывается перед каждым
/// прогоном агента и может подмешать в вызов три вещи: инструкции (довесок к системному промпту),
/// сообщения и инструменты. После прогона ему отдают запрос, ответ и исключение — так провайдеры
/// ведут своё состояние. Всё интересное во фреймворке (компакция, скиллы, todo, файлы, память,
/// RAG) реализовано именно так.
/// </summary>
internal static class AgentContextProviderFactory
{
    /// <param name="definition">
    /// Агент из конструктора или null для встроенного. От него зависит только набор скиллов:
    /// у встроенного их нет вообще, у своего — ровно отмеченные в конструкторе. Остальные
    /// провайдеры общие для всех агентов и настраиваются в appsettings.
    /// </param>
    public static AgentProviderSet Create(
        AssistantOptions options,
        AssistantAgentDefinition? definition,
        IChatClient chatClient,
        OpenSearchTextSearchClient? searchClient,
        ProcessSkillScriptRunner? scriptRunner,
        InMemoryDocumentStore? documentStore,
        ILoggerFactory loggerFactory)
    {
        var providers = new List<AIContextProvider>();
        var descriptions = new List<string>();

        // Инструменты документов выдаются на каждый прогон и только если к чату что-то приложено,
        // поэтому провайдер добавляется всегда — решение принимает он сам.
        if (documentStore is not null)
        {
            providers.Add(new DocumentToolsProvider(documentStore));
            descriptions.Add("документы: по требованию");
        }

        AddCompaction(options, chatClient, loggerFactory, providers, descriptions);
        AddSkills(options, definition, scriptRunner, loggerFactory, providers, descriptions);
        AddTodo(options, providers, descriptions);
        AddModes(options, providers, descriptions);
        AddFiles(options, providers, descriptions);
        AddMemory(options, providers, descriptions);
        AddSearch(options, searchClient, loggerFactory, providers, descriptions);

        return new AgentProviderSet { Providers = providers, Descriptions = descriptions };
    }

    /// <summary>
    /// Сжатие истории. Ставим первым: остальные провайдеры дописывают в контекст свои сообщения,
    /// и сжимать надо именно исходную переписку, а не свежевставленные инструкции.
    /// </summary>
    private static void AddCompaction(
        AssistantOptions options,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        var strategy = CompactionStrategyFactory.Create(
            options.Compaction,
            options.Generation.MaxOutputTokens,
            chatClient);

        if (strategy is null)
            return;

        providers.Add(new CompactionProvider(strategy, stateKey: null, loggerFactory));
        descriptions.Add($"компакция: {CompactionStrategyFactory.Describe(options.Compaction)}");
    }

    /// <summary>
    /// Скиллы. В системный промпт уезжает только список «имя — описание», тело скилла модель
    /// подтягивает инструментом load_skill. Несуществующие папки молча пропускаем — иначе
    /// приложение не поднимется из-за отсутствующей папки со скиллами.
    /// </summary>
    private static void AddSkills(
        AssistantOptions options,
        AssistantAgentDefinition? definition,
        ProcessSkillScriptRunner? scriptRunner,
        ILoggerFactory loggerFactory,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Skills.Enabled)
            return;

        var directories = ResolveSkillDirectories(options);

        if (directories.Length == 0)
            return;

        // Скиллы есть только у агентов из конструктора и только те, что там отмечены.
        //
        // У встроенного агента (definition == null) скиллов нет по определению: он универсальный
        // и ничего специфического уметь не должен — за специализацию отвечают агенты, которые
        // пользователь собирает сам. Пустой список у своего агента — то же самое: осознанный
        // выбор не давать ему скиллов.
        var allowed = new HashSet<string>(definition?.Skills ?? [], StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return;

        var builder = new AgentSkillsProviderBuilder()
            .UseFileSkills(directories, CreateFileOptions(options))
            .UseLoggerFactory(loggerFactory)
            .UseOptions(skillOptions =>
            {
                // Подтверждения на чтение скиллов не нужны: это просто markdown из нашей же папки.
                skillOptions.DisableLoadSkillApproval = true;
                skillOptions.DisableReadSkillResourceApproval = true;
                skillOptions.DisableRunSkillScriptApproval = !options.Approvals.Enabled;
            });

        // Раннер отдаём всегда: билдер требует его безусловно, даже когда ни один скилл
        // скриптов не содержит. При выключенных скриптах подставляем заглушку — до неё всё
        // равно не дойдёт, потому что скрипты в этом режиме просто не обнаруживаются.
        builder = builder.UseFileScriptRunner(options.Skills.AllowScripts && scriptRunner is not null
            ? scriptRunner.AsRunner()
            : ProcessSkillScriptRunner.Disabled);

        builder = builder.UseFilter((skill, _) => allowed.Contains(skill.Frontmatter.Name));

        providers.Add(builder.Build());

        descriptions.Add($"скиллы: {string.Join(", ", allowed)}");
    }

    /// <summary>Папки со скиллами, которые реально существуют. Отсутствующие молча пропускаем.</summary>
    internal static string[] ResolveSkillDirectories(AssistantOptions options) =>
        [
            .. options.Skills.Directories
                .Select(Path.GetFullPath)
                .Where(Directory.Exists),
        ];

    internal static AgentFileSkillsSourceOptions CreateFileOptions(AssistantOptions options) => new()
    {
        SearchDepth = options.Skills.SearchDepth,
        AllowedScriptExtensions = options.Skills.ScriptExtensions.Count > 0
            ? options.Skills.ScriptExtensions
            : null,

        // Скрипты выключены — не обнаруживаем их вовсе, чтобы ни один скилл не мог ничего
        // запустить. Сам инструмент run_skill_script модели всё равно предлагается: провайдер
        // регистрирует свои три инструмента независимо от того, есть ли у скиллов скрипты.
        // Вреда нет — запускать нечего, а раннер подменён заглушкой; но одно лишнее описание
        // в промпте это стоит.
        ScriptFilter = options.Skills.AllowScripts ? null : _ => false,
    };

    /// <summary>Todo-лист: модель сама разбивает длинную задачу на пункты и закрывает их по ходу.</summary>
    private static void AddTodo(
        AssistantOptions options,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Todo.Enabled)
            return;

        providers.Add(new TodoProvider(new TodoProviderOptions
        {
            SuppressTodoListMessage = options.Todo.SuppressTodoListMessage,
        }));

        descriptions.Add("todo-лист");
    }

    /// <summary>Режимы работы: модель переключается между plan и execute своим инструментом.</summary>
    private static void AddModes(
        AssistantOptions options,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Modes.Enabled)
            return;

        var modes = options.Modes.Modes.Count > 0 ? options.Modes.Modes : ModesOptions.Default;

        providers.Add(new AgentModeProvider(new AgentModeProviderOptions
        {
            DefaultMode = options.Modes.DefaultMode,
            Modes = [.. modes.Select(mode => new AgentModeProviderOptions.AgentMode(mode.Name, mode.Instructions))],
        }));

        descriptions.Add($"режимы: {string.Join("/", modes.Select(mode => mode.Name))}");
    }

    /// <summary>
    /// Файловые инструменты. Стор в памяти — безопасный вариант по умолчанию: агент может
    /// складывать промежуточные результаты, но за пределы процесса они не выходят.
    /// </summary>
    private static void AddFiles(
        AssistantOptions options,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Files.Enabled)
            return;

        var store = CreateStore(options.Files.RootDirectory);

        providers.Add(new FileAccessProvider(store, new FileAccessProviderOptions
        {
            DisableWriteTools = options.Files.ReadOnly,
            DisableReadOnlyToolApproval = !options.Approvals.Enabled || options.Approvals.AutoApproveReadOnly,
            DisableWriteToolApproval = !options.Approvals.Enabled,
        }));

        descriptions.Add("файлы: "
            + (options.Files.RootDirectory is null ? "в памяти" : options.Files.RootDirectory)
            + (options.Files.ReadOnly ? ", только чтение" : string.Empty));
    }

    /// <summary>
    /// Долговременная память: модель сама пишет заметки и потом их ищет. Держим по папке
    /// на пользователя, чтобы заметки одного человека не утекали другому.
    /// </summary>
    private static void AddMemory(
        AssistantOptions options,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Memory.Enabled)
            return;

        var store = CreateStore(options.Memory.RootDirectory);
        var perUser = options.Memory.PerUserFolder;

        providers.Add(new FileMemoryProvider(
            store,
            session => new FileMemoryState { WorkingFolder = BuildMemoryFolder(session, perUser) }));

        descriptions.Add("память: "
            + (options.Memory.RootDirectory is null ? "в памяти процесса" : options.Memory.RootDirectory)
            + (perUser ? ", по папке на пользователя" : string.Empty));
    }

    /// <summary>RAG: либо поиск перед каждым запросом, либо инструмент поиска для модели.</summary>
    private static void AddSearch(
        AssistantOptions options,
        OpenSearchTextSearchClient? searchClient,
        ILoggerFactory loggerFactory,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Search.Enabled || searchClient is null)
            return;

        providers.Add(new TextSearchProvider(
            searchClient.SearchAsync,
            new TextSearchProviderOptions
            {
                SearchTime = options.Search.SearchBeforeEveryRequest
                    ? TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke
                    : TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
                FunctionToolName = "search_knowledge_base",
                FunctionToolDescription =
                    "Ищет во внутренней базе знаний. Вызывай, когда вопрос про внутренние документы, "
                    + "регламенты или данные компании.",
            },
            loggerFactory));

        descriptions.Add($"поиск: OpenSearch/{options.Search.Index}"
            + (options.Search.SearchBeforeEveryRequest ? ", перед каждым запросом" : ", по решению модели"));
    }

    private static AgentFileStore CreateStore(string? rootDirectory) => string.IsNullOrWhiteSpace(rootDirectory)
        ? new InMemoryAgentFileStore()
        : new FileSystemAgentFileStore(Path.GetFullPath(rootDirectory));

    /// <summary>
    /// StateBag типизирован по ссылочным типам, поэтому идентификаторы лежат в нём строками.
    /// </summary>
    private static string BuildMemoryFolder(AgentSession? session, bool perUser)
    {
        if (!perUser || session is null)
            return "shared";

        return session.StateBag.TryGetValue<string>(AssistantSessionKeys.UserId, out var userId)
            && !string.IsNullOrWhiteSpace(userId)
                ? $"users/{userId}"
                : "shared";
    }
}
