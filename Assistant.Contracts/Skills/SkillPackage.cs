namespace Assistant.Contracts.Skills;

/// <summary>Лимиты на zip-архив скилла. Одни и те же при загрузке и при распаковке.</summary>
public static class SkillPackageLimits
{
    /// <summary>Потолок размера самого архива.</summary>
    public const long MaxArchiveBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Потолок суммарного размера распакованного. Отдельно от размера архива, потому что
    /// zip сжимает: десять килобайт архива легко разворачиваются в гигабайт нулей.
    /// </summary>
    public const long MaxUncompressedBytes = 25 * 1024 * 1024;

    public const long MaxEntryBytes = 5 * 1024 * 1024;

    public const int MaxEntries = 300;

    /// <summary>Обязательный файл в корне пакета.</summary>
    public const string ManifestFileName = "SKILL.md";

    /// <summary>
    /// Расширения, которые фреймворк считает скриптами. Пользовательским скиллам их всё
    /// равно не дают запускать — список нужен, чтобы пометить такой пакет флагом.
    /// </summary>
    public static IReadOnlySet<string> ScriptExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".py", ".js", ".mjs", ".cjs", ".sh", ".bash", ".zsh", ".ps1", ".cs", ".csx", ".rb", ".pl", ".php",
        };

    /// <summary>
    /// Расширения, которые в скилл не пускаем вовсе. Скилл — это инструкции и данные к ним;
    /// готовый бинарник внутри означает, что кто-то принёс на сервер исполняемый файл.
    /// </summary>
    public static IReadOnlySet<string> ForbiddenExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".so", ".dylib", ".bat", ".cmd", ".com", ".msi", ".scr", ".jar", ".bin", ".apk",
        };
}

/// <summary>Что удалось вычитать из архива, не распаковывая его на диск.</summary>
public sealed record SkillPackageInfo
{
    /// <summary>Имя из frontmatter SKILL.md.</summary>
    public required string Name { get; init; }

    /// <summary>Описание из frontmatter SKILL.md.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Общий префикс путей внутри архива. Пусто — SKILL.md лежит в корне; иначе архив
    /// собран из папки, и всё содержимое лежит под её именем.
    /// </summary>
    public required string RootPrefix { get; init; }

    public required int EntryCount { get; init; }

    public required long UncompressedBytes { get; init; }

    /// <summary>В пакете есть файлы со скриптовыми расширениями.</summary>
    public required bool HasScripts { get; init; }
}

/// <summary>
/// Результат разбора архива. Не исключение: битый архив — это ожидаемый ответ пользователю,
/// а не сбой приложения.
/// </summary>
public sealed record SkillPackageResult
{
    public string? Error { get; private init; }

    public SkillPackageInfo? Package { get; private init; }

    public bool IsSuccess => Package is not null;

    public static SkillPackageResult Success(SkillPackageInfo package) => new() { Package = package };

    public static SkillPackageResult Failure(string error) => new() { Error = error };
}
