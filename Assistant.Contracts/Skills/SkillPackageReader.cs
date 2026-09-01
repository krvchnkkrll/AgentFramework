using System.IO.Compression;
using System.Text;

namespace Assistant.Contracts.Skills;

/// <summary>
/// Разбор и распаковка zip-архива со скиллом.
///
/// Лежит в контрактах, потому что нужен по обе стороны и в обеих ролях: слой Application
/// разбирает архив при загрузке (вычитать frontmatter, отбить мусор), слой ассистента
/// распаковывает его в кэш перед работой агента. Держать две реализации нельзя — проверки
/// разъедутся, и на диск попадёт то, что не прошло бы при загрузке.
///
/// Про безопасность. Архив приносит пользователь, поэтому здесь закрыты три классические
/// дыры: zip slip (запись по пути с «..» или абсолютным), zip-бомба (лимит на распакованный
/// размер и число записей) и протаскивание исполняемых файлов. Скрипты внутри пакета
/// распаковываются, но помечаются флагом — запускать их пользовательским скиллам нельзя,
/// это решается на уровне провайдера скиллов, а не здесь.
/// </summary>
public static class SkillPackageReader
{
    /// <summary>Разбирает архив, ничего не записывая на диск.</summary>
    public static SkillPackageResult Read(Stream archiveStream)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);

        ZipArchive archive;

        try
        {
            archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return SkillPackageResult.Failure("Файл не является zip-архивом.");
        }

        using (archive)
        {
            var manifest = FindManifest(archive);

            if (manifest is null)
            {
                return SkillPackageResult.Failure(
                    $"В архиве нет файла {SkillPackageLimits.ManifestFileName} — без него это не скилл.");
            }

            var rootPrefix = GetRootPrefix(manifest.FullName);

            var entryCount = 0;
            var totalBytes = 0L;
            var hasScripts = false;

            foreach (var entry in archive.Entries)
            {
                var path = Normalize(entry.FullName);

                // Папки приезжают отдельными записями нулевого размера — считать их незачем.
                if (path.Length == 0 || path.EndsWith('/') || IsJunk(path))
                    continue;

                if (!IsSafePath(path))
                    return SkillPackageResult.Failure($"Недопустимый путь внутри архива: {entry.FullName}");

                if (rootPrefix.Length > 0 && !path.StartsWith(rootPrefix, StringComparison.Ordinal))
                {
                    return SkillPackageResult.Failure(
                        $"В архиве больше одной корневой папки — оставьте только папку со скиллом ({entry.FullName}).");
                }

                var extension = Path.GetExtension(path);

                if (SkillPackageLimits.ForbiddenExtensions.Contains(extension))
                    return SkillPackageResult.Failure($"Файлы {extension} внутрь скилла класть нельзя: {entry.FullName}");

                if (SkillPackageLimits.ScriptExtensions.Contains(extension))
                    hasScripts = true;

                if (entry.Length > SkillPackageLimits.MaxEntryBytes)
                {
                    return SkillPackageResult.Failure(
                        $"Файл {entry.FullName} больше {SkillPackageLimits.MaxEntryBytes / 1024 / 1024} МБ.");
                }

                entryCount++;
                totalBytes += entry.Length;

                if (entryCount > SkillPackageLimits.MaxEntries)
                    return SkillPackageResult.Failure($"В архиве больше {SkillPackageLimits.MaxEntries} файлов.");

                if (totalBytes > SkillPackageLimits.MaxUncompressedBytes)
                {
                    return SkillPackageResult.Failure(
                        $"Распакованный скилл больше {SkillPackageLimits.MaxUncompressedBytes / 1024 / 1024} МБ.");
                }
            }

            string manifestText;

            using (var reader = new StreamReader(manifest.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                manifestText = reader.ReadToEnd();

            var frontmatter = FrontmatterParser.Parse(manifestText);

            if (!frontmatter.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                return SkillPackageResult.Failure(
                    $"В {SkillPackageLimits.ManifestFileName} не заполнено поле name во frontmatter.");
            }

            if (!frontmatter.TryGetValue("description", out var description) || string.IsNullOrWhiteSpace(description))
            {
                return SkillPackageResult.Failure(
                    $"В {SkillPackageLimits.ManifestFileName} не заполнено поле description во frontmatter. "
                    + "По нему модель решает, брать скилл в работу или нет.");
            }

            return SkillPackageResult.Success(new SkillPackageInfo
            {
                Name = name.Trim(),
                Description = description.Trim(),
                RootPrefix = rootPrefix,
                EntryCount = entryCount,
                UncompressedBytes = totalBytes,
                HasScripts = hasScripts,
            });
        }
    }

    /// <summary>
    /// Распаковывает архив в указанную папку, срезая общий корневой префикс: на выходе
    /// <see cref="SkillPackageLimits.ManifestFileName"/> всегда лежит прямо в
    /// <paramref name="targetDirectory"/>, независимо от того, как архив был собран.
    ///
    /// Все проверки из <see cref="Read"/> здесь повторяются: между загрузкой и распаковкой
    /// файл лежал в хранилище, и полагаться на то, что он не изменился, нельзя.
    /// </summary>
    /// <returns>Ошибка или null, если всё разложилось.</returns>
    public static string? ExtractTo(Stream archiveStream, string targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        var read = Read(archiveStream);

        if (!read.IsSuccess)
            return read.Error;

        var package = read.Package!;

        if (archiveStream.CanSeek)
            archiveStream.Seek(0, SeekOrigin.Begin);

        var root = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(root);

        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);

        var written = 0L;

        foreach (var entry in archive.Entries)
        {
            var path = Normalize(entry.FullName);

            if (path.Length == 0 || path.EndsWith('/') || IsJunk(path) || !IsSafePath(path))
                continue;

            var relative = package.RootPrefix.Length > 0 && path.StartsWith(package.RootPrefix, StringComparison.Ordinal)
                ? path[package.RootPrefix.Length..]
                : path;

            if (relative.Length == 0)
                continue;

            var destination = Path.GetFullPath(Path.Combine(root, relative));

            // Последний рубеж против zip slip: даже если проверка пути выше что-то пропустила,
            // за пределы целевой папки запись не уедет.
            if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return $"Недопустимый путь внутри архива: {entry.FullName}";

            written += entry.Length;

            if (written > SkillPackageLimits.MaxUncompressedBytes)
            {
                return $"Распакованный скилл больше "
                    + $"{SkillPackageLimits.MaxUncompressedBytes / 1024 / 1024} МБ.";
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        return File.Exists(Path.Combine(root, SkillPackageLimits.ManifestFileName))
            ? null
            : $"После распаковки в скилле не оказалось {SkillPackageLimits.ManifestFileName}.";
    }

    /// <summary>
    /// Ищет SKILL.md, лежащий ближе всего к корню. Глубже он может встретиться внутри
    /// ресурсов скилла — такой брать нельзя, иначе корнем пакета окажется подпапка.
    /// </summary>
    private static ZipArchiveEntry? FindManifest(ZipArchive archive) => archive.Entries
        .Where(entry => string.Equals(
            Path.GetFileName(Normalize(entry.FullName)),
            SkillPackageLimits.ManifestFileName,
            StringComparison.OrdinalIgnoreCase))
        .Where(entry => !IsJunk(Normalize(entry.FullName)) && IsSafePath(Normalize(entry.FullName)))
        .OrderBy(entry => Normalize(entry.FullName).Count(character => character == '/'))
        .FirstOrDefault();

    /// <summary>Часть пути до SKILL.md — то, что при распаковке срезается.</summary>
    private static string GetRootPrefix(string manifestPath)
    {
        var path = Normalize(manifestPath);
        var separator = path.LastIndexOf('/');

        return separator < 0 ? string.Empty : path[..(separator + 1)];
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <summary>
    /// Служебный мусор архиваторов. Без этого фильтра папка, заархивированная в macOS
    /// через Finder, не проходит проверку корня: рядом со скиллом лежат __MACOSX и
    /// .DS_Store, и архив выглядит как несколько корневых папок сразу.
    /// </summary>
    private static bool IsJunk(string normalizedPath)
    {
        var segments = normalizedPath.Split('/');

        if (segments.Any(segment => segment is "__MACOSX" or ".git"))
            return true;

        var name = segments[^1];

        return name is ".DS_Store" or "Thumbs.db" or "desktop.ini" || name.StartsWith("._", StringComparison.Ordinal);
    }

    private static bool IsSafePath(string normalizedPath)
    {
        if (normalizedPath.Length == 0 || Path.IsPathRooted(normalizedPath) || normalizedPath.Contains(':'))
            return false;

        return !normalizedPath
            .Split('/')
            .Any(segment => segment is ".." or "." || segment.Length == 0);
    }
}

/// <summary>
/// Разбор YAML-frontmatter в начале SKILL.md. Полноценный YAML здесь не нужен и вреден:
/// от манифеста требуются ровно два поля верхнего уровня, всё остальное (metadata и прочее)
/// сознательно пропускается.
/// </summary>
internal static class FrontmatterParser
{
    public static Dictionary<string, string> Parse(string markdown)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(markdown))
            return result;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var start = -1;

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Trim().Length == 0)
                continue;

            // Frontmatter обязан открываться первой же непустой строкой, иначе его нет.
            if (lines[index].Trim() == "---")
                start = index + 1;

            break;
        }

        if (start < 0)
            return result;

        for (var index = start; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.Trim() == "---")
                break;

            // Строки с отступом — это вложенные ключи (metadata и т.п.), нам они не нужны.
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
                continue;

            var separator = line.IndexOf(':');

            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            // Блочный скаляр: значение лежит на следующих строках с отступом.
            if (value is "|" or ">" or "|-" or ">-")
            {
                var builder = new StringBuilder();

                while (index + 1 < lines.Length
                    && lines[index + 1].Length > 0
                    && char.IsWhiteSpace(lines[index + 1][0]))
                {
                    if (builder.Length > 0)
                        builder.Append(' ');

                    builder.Append(lines[++index].Trim());
                }

                value = builder.ToString();
            }

            result[key] = Unquote(value);
        }

        return result;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
