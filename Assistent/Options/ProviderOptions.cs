namespace Assistant.Options;

/// <summary>
/// Скиллы — папки с файлом SKILL.md внутри. В системный промпт уезжает только список
/// «имя — описание», а тело скилла модель подтягивает инструментом load_skill, когда решит,
/// что скилл ей нужен. Это позволяет держать десятки инструкций, не раздувая каждый запрос.
/// </summary>
public sealed class SkillsOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Папки, в которых лежат скиллы. Относительные пути считаются от рабочей директории процесса
    /// (для Web это папка с Web.csproj при запуске из IDE).
    ///
    /// По умолчанию пусто, а не ["skills"], и это принципиально: пустой JSON-массив не порождает
    /// ни одного ключа конфигурации, поэтому "Directories": [] для биндера неотличимо от
    /// отсутствующего ключа — он просто оставит значение по умолчанию. Был бы дефолт непустым,
    /// выключить скиллы через пустой список стало бы невозможно: в appsettings пишешь «пусто»,
    /// а получаешь дефолт. Реальный список живёт в appsettings.
    /// </summary>
    public IReadOnlyList<string> Directories { get; init; } = [];

    /// <summary>
    /// Разрешить скиллам иметь скрипты и дать модели инструмент run_skill_script.
    /// Скрипты выполняет наш собственный раннер — см. AgentSkillScriptRunner.
    /// </summary>
    public bool AllowScripts { get; init; }

    /// <summary>
    /// Расширения файлов, которые считаются скриптами. Пусто — берутся значения по умолчанию
    /// фреймворка (.py, .js, .sh, .ps1, .cs, .csx).
    /// </summary>
    public IReadOnlyList<string> ScriptExtensions { get; init; } = [];

    /// <summary>Глубина обхода папки скилла в поисках ресурсов и скриптов.</summary>
    public int SearchDepth { get; init; } = 3;
}

/// <summary>Встроенные инструменты агента.</summary>
public sealed class ToolsOptions
{
    /// <summary>
    /// Искусственная задержка ответа инструментов, в секундах. 0 — выключено.
    ///
    /// Только для демонстраций: встроенные инструменты отрабатывают мгновенно, и увидеть
    /// в интерфейсе плашку «агент вызывает get_current_time» на них невозможно. Задержка
    /// слушает токен отмены, поэтому кнопка «стоп» продолжает работать.
    /// </summary>
    public int SimulatedDelaySeconds { get; init; }
}

/// <summary>Todo-лист агента: модель сама ведёт список подзадач по ходу длинной работы.</summary>
public sealed class TodoOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>Не подмешивать текущий список задач в контекст (оставить только инструменты).</summary>
    public bool SuppressTodoListMessage { get; init; }
}

/// <summary>
/// Режимы работы агента. Модель переключает режим инструментом, а мы можем прочитать текущий
/// режим снаружи и, например, не давать агенту писать в БД, пока он в режиме планирования.
/// </summary>
public sealed class ModesOptions
{
    public bool Enabled { get; init; }

    public string DefaultMode { get; init; } = "execute";

    /// <summary>
    /// Список режимов. Пусто — берутся <see cref="Default"/>.
    ///
    /// Дефолт здесь обязан быть пустым: биндер конфигурации не заменяет непустую коллекцию,
    /// а дописывает к ней значения из JSON. С непустым дефолтом два режима из appsettings
    /// превратились бы в четыре — два своих и два вшитых.
    /// </summary>
    public IReadOnlyList<AgentModeOption> Modes { get; init; } = [];

    /// <summary>Режимы по умолчанию, если в конфигурации ничего не задано.</summary>
    public static IReadOnlyList<AgentModeOption> Default { get; } =
    [
        new()
        {
            Name = "plan",
            Instructions = "Режим планирования. Изучай задачу, задавай уточняющие вопросы и предлагай план. "
                + "Не вызывай инструменты, меняющие состояние. Когда план согласован — переключись в режим execute.",
        },
        new()
        {
            Name = "execute",
            Instructions = "Режим исполнения. Делай задачу по согласованному плану, вызывай нужные инструменты "
                + "и отчитывайся о результате.",
        },
    ];
}

/// <summary>Один режим работы агента.</summary>
public sealed class AgentModeOption
{
    public required string Name { get; init; }

    public required string Instructions { get; init; }
}

/// <summary>
/// Файловые инструменты: write, read, delete, ls, replace, replace_lines, grep.
/// Файлы живут в «сторе» — либо в памяти процесса, либо в папке на диске.
/// </summary>
public sealed class FilesOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Папка-корень файлового стора. Пусто — стор в памяти процесса (после рестарта всё пропадает),
    /// это безопасный вариант по умолчанию. Агент физически не может выйти за пределы этой папки.
    /// </summary>
    public string? RootDirectory { get; init; }

    /// <summary>Оставить только чтение: read, ls, grep. Запись и удаление модели недоступны.</summary>
    public bool ReadOnly { get; init; }
}

/// <summary>
/// Долговременная память агента поверх файлового стора: модель сама решает, что записать,
/// и потом ищет по своим заметкам. Индекс заметок лежит в memories.md внутри стора.
/// </summary>
public sealed class MemoryOptions
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Папка-корень для памяти. Пусто — память в памяти процесса, то есть бесполезна между
    /// перезапусками. Для настоящей памяти задайте путь на диске.
    /// </summary>
    public string? RootDirectory { get; init; }

    /// <summary>
    /// Держать заметки в подпапке на каждого пользователя. Требует, чтобы в запросе был UserId.
    /// </summary>
    public bool PerUserFolder { get; init; } = true;
}

/// <summary>
/// Подтверждение вызовов инструментов пользователем. Инструменты из
/// <see cref="RequireApprovalFor"/> оборачиваются так, что перед вызовом агент останавливается
/// и просит подтверждения, а <see cref="AutoApproveReadOnly"/> заранее разрешает безобидные чтения.
/// </summary>
public sealed class ApprovalsOptions
{
    public bool Enabled { get; init; }

    /// <summary>Имена инструментов, требующих подтверждения. Пусто — подтверждать нечего.</summary>
    public IReadOnlyList<string> RequireApprovalFor { get; init; } = [];

    /// <summary>Автоматически одобрять инструменты чтения файлового стора и скиллов.</summary>
    public bool AutoApproveReadOnly { get; init; } = true;
}

/// <summary>
/// RAG поверх OpenSearch. Ходим обычным HTTP в _search — клиент Elasticsearch тут не подходит:
/// начиная с 8.x он проверяет заголовок x-elastic-product и на OpenSearch падает.
/// </summary>
public sealed class SearchOptions
{
    public bool Enabled { get; init; }

    /// <summary>Базовый адрес OpenSearch, например http://192.168.0.14:9200.</summary>
    public string? Url { get; init; }

    /// <summary>Индекс или алиас, по которому ищем.</summary>
    public string Index { get; init; } = "knowledge";

    public string? Username { get; init; }

    public string? Password { get; init; }

    /// <summary>
    /// Поля, по которым идёт полнотекстовый поиск. Пусто — берутся <see cref="DefaultFields"/>.
    /// Дефолт пустой по той же причине, что и у режимов: биндер дописывает к непустой коллекции,
    /// а не заменяет её.
    /// </summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>Поля поиска по умолчанию, если в конфигурации ничего не задано.</summary>
    public static IReadOnlyList<string> DefaultFields { get; } = ["title^2", "content"];

    /// <summary>Поле документа, из которого берётся текст для модели.</summary>
    public string TextField { get; init; } = "content";

    /// <summary>Поле документа с названием источника (для ссылок в ответе).</summary>
    public string? TitleField { get; init; } = "title";

    /// <summary>Поле документа со ссылкой на источник.</summary>
    public string? LinkField { get; init; } = "url";

    public int MaxResults { get; init; } = 5;

    /// <summary>
    /// true — поиск выполняется сам перед каждым запросом к модели и результаты подмешиваются
    /// в контекст. false — модели даётся инструмент поиска, и она решает, звать его или нет.
    /// На медленной локальной модели второй вариант обычно приятнее.
    /// </summary>
    public bool SearchBeforeEveryRequest { get; init; }

    /// <summary>Таймаут запроса в OpenSearch.</summary>
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Не проверять TLS-сертификат (для self-signed в локальном контуре).</summary>
    public bool AllowInvalidCertificate { get; init; }
}
