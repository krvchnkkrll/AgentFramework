using Assistant.Compaction;
using Assistant.Contracts.Models;
using Assistant.Documents;
using Assistant.Options;
using Assistant.Search;
using Assistant.Skills;
using FileService.Contracts;
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
///
/// Сама сборка синхронная: здесь провайдеры только создаются, ни в сеть, ни в базу никто не
/// ходит. Асинхронная работа происходит позже, внутри провайдеров, во время прогона, — например,
/// текст скилла качается из файлового сервиса, когда модель вызывает load_skill.
/// </summary>
internal static class AgentContextProviderFactory
{
    /// <summary>
    /// Системный промпт провайдера скиллов. Свой, а не встроенный: встроенный на английском
    /// и рассказывает модели про скрипты, которых у нас нет. Вместо {skills} фреймворк
    /// подставит список «имя — описание».
    /// </summary>
    private const string SkillsPrompt =
        """
        У тебя есть скиллы — готовые инструкции для отдельных видов задач. Пока ты видишь только их имена и описания:

        {skills}

        Как работать со скиллами:
        - Если задача подходит под описание скилла, сначала вызови load_skill с его именем, а потом делай задачу по полученной инструкции.
        - В тексте скилла могут быть ссылки на другие скиллы из этого списка. Если такой скилл нужен для задачи, загрузи его тем же load_skill по имени.
        - Загружай только то, что нужно для текущей задачи. Не выдумывай содержимое скилла, пока не загрузил его.
        """;

    /// <param name="definition">
    /// Агент из конструктора или null для встроенного. От него зависит только набор скиллов:
    /// у встроенного их нет вообще, у своего — ровно отмеченные в конструкторе. Остальные
    /// провайдеры общие для всех агентов и настраиваются в appsettings.
    /// </param>
    /// <param name="fileService">
    /// Откуда качать тексты скиллов. null — файловый сервис не подключён, и агенты работают
    /// без скиллов.
    /// </param>
    public static AgentProviderSet Create(
        AssistantOptions options,
        AssistantAgentDefinition? definition,
        IChatClient chatClient,
        OpenSearchTextSearchClient? searchClient,
        IFileService? fileService,
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
        AddSkills(options, definition, fileService, loggerFactory, providers, descriptions);
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
    /// Скиллы. Провайдер встроенный (<see cref="AgentSkillsProvider"/>), а скиллы свои —
    /// <see cref="FileServiceSkill"/>: имя и описание из базы, текст из файлового сервиса.
    ///
    /// В системный промпт уезжает только список «имя — описание», текст модель подтягивает
    /// инструментом load_skill, когда решит, что скилл ей нужен.
    /// </summary>
    private static void AddSkills(
        AssistantOptions options,
        AssistantAgentDefinition? definition,
        IFileService? fileService,
        ILoggerFactory loggerFactory,
        List<AIContextProvider> providers,
        List<string> descriptions)
    {
        if (!options.Skills.Enabled || fileService is null)
            return;

        // Скиллы есть только у агентов из конструктора и только те, что там отмечены.
        //
        // У встроенного агента (definition == null) скиллов нет по определению: он универсальный
        // и ничего специфического уметь не должен — за специализацию отвечают агенты, которые
        // пользователь собирает сам. Пустой список у своего агента — то же самое: осознанный
        // выбор не давать ему скиллов.
        if (definition is null || definition.Skills.Count == 0)
            return;

        var logger = loggerFactory.CreateLogger<FileServiceSkill>();

        FileServiceSkill[] skills =
        [
            .. definition.Skills
                .Select(skill => FileServiceSkill.TryCreate(skill, fileService, logger))
                .OfType<FileServiceSkill>(),
        ];

        if (skills.Length == 0)
            return;

        var provider = new AgentSkillsProviderBuilder()
            .UseSkills(skills)
            .UsePromptTemplate(SkillsPrompt)
            .UseLoggerFactory(loggerFactory)
            .UseOptions(skillOptions =>
            {
                // Подтверждения не нужны: скилл — это просто текст, который модель читает.
                skillOptions.DisableLoadSkillApproval = true;
                skillOptions.DisableReadSkillResourceApproval = true;

                // read_skill_resource и run_skill_script провайдер выдаёт модели всегда, отключить
                // их нельзя. Ресурсов и скриптов у наших скиллов нет, так что вызов просто вернёт
                // «не найдено». Без этого флага он бы ещё и остановил агента в ожидании
                // подтверждения от пользователя.
                skillOptions.DisableRunSkillScriptApproval = true;
            })
            .Build();

        providers.Add(provider);

        descriptions.Add($"скиллы: {string.Join(", ", skills.Select(skill => skill.Frontmatter.Name))}");
    }

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
