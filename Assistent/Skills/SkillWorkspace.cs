using System.Collections.Concurrent;
using Assistant.Contracts.Models;
using Assistant.Contracts.Skills;
using Assistant.Options;
using Microsoft.Extensions.Logging;

namespace Assistant.Skills;

/// <summary>
/// Разворачивает архивы скиллов в локальный кэш и отдаёт агенту пути к готовым папкам.
///
/// Фреймворк умеет читать скиллы только с диска, а хранятся они в файловом хранилище —
/// этот класс и есть мост между тем и другим.
///
/// Раскладка кэша: {root}/{id скилла}/{хэш содержимого}. Ключ — именно содержимое, а не
/// агент и не чат, и это важно по трём причинам:
///
/// 1. Один скилл выдан десяти агентам в сотне чатов — на диске он лежит в одном экземпляре.
/// 2. Загрузили новую версию — поменялся хэш, значит поменялся путь, и распаковка происходит
///    сама собой. Инвалидировать кэш отдельно не нужно.
/// 3. Старая версия остаётся на месте, пока её не уберёт чистильщик, поэтому агент, который
///    прямо сейчас работает со старой папкой, не обнаружит, что она исчезла из-под него.
///
/// Папки кэша только для чтения. Скрипты скиллов, если их когда-нибудь разрешат, должны
/// писать не сюда, а в отдельную рабочую директорию: общая на все чаты папка означала бы,
/// что данные одного чата видны другому.
/// </summary>
public sealed class SkillWorkspace : IDisposable
{
    /// <summary>Имя папки, в которую распаковываем перед тем, как переставить её на место.</summary>
    private const string StagingPrefix = ".staging-";

    private readonly ISkillPackageSource? _source;
    private readonly ILogger<SkillWorkspace> _logger;
    private readonly string _root;

    /// <summary>
    /// По замку на скилл: два параллельных сообщения не должны распаковывать один архив
    /// одновременно. Замок именно на скилл, а не общий, — распаковка небыстрая, и держать
    /// на ней все чаты сразу незачем.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public SkillWorkspace(
        AssistantOptions options,
        ISkillPackageSource? source,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _source = source;
        _logger = loggerFactory.CreateLogger<SkillWorkspace>();
        _root = ResolveRoot(options.Skills.CacheDirectory);
    }

    /// <summary>Корень кэша. Нужен чистильщику.</summary>
    public string Root => _root;

    /// <summary>
    /// Готовит папки для переданных скиллов и возвращает пути к ним.
    ///
    /// Скилл, который не удалось развернуть, молча выпадает из списка: агент останется без
    /// него, но ответит. Ронять чат из-за одного битого архива — хуже.
    /// </summary>
    public async Task<IReadOnlyList<string>> MaterializeAsync(
        IReadOnlyList<AssistantSkillReference> skills,
        CancellationToken cancellationToken = default)
    {
        if (skills.Count == 0 || _source is null)
            return [];

        var directories = new List<string>(skills.Count);

        foreach (var skill in skills)
        {
            var directory = await MaterializeOneAsync(skill, cancellationToken);

            if (directory is not null)
                directories.Add(directory);
        }

        return directories;
    }

    private async Task<string?> MaterializeOneAsync(
        AssistantSkillReference skill,
        CancellationToken cancellationToken)
    {
        var skillRoot = Path.Combine(_root, skill.Id.ToString("N"));
        var target = Path.Combine(skillRoot, skill.ContentHash);

        if (Directory.Exists(target))
        {
            Touch(target);
            return target;
        }

        var gate = _locks.GetOrAdd(target, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            // Пока ждали замок, папку мог развернуть соседний запрос.
            if (Directory.Exists(target))
            {
                Touch(target);
                return target;
            }

            return await ExtractAsync(skill, skillRoot, target, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> ExtractAsync(
        AssistantSkillReference skill,
        string skillRoot,
        string target,
        CancellationToken cancellationToken)
    {
        await using var package = await _source!.OpenAsync(skill.FileId, cancellationToken);

        if (package is null)
        {
            _logger.LogWarning(
                "Скилл {SkillName} ({SkillId}): файла {FileId} нет в хранилище, агент останется без него.",
                skill.Name,
                skill.Id,
                skill.FileId);

            return null;
        }

        // Архив нужно читать дважды — сначала разбор, потом распаковка, — а поток из
        // хранилища перемотать можно не всякий. Такой копируем во временный файл.
        var (archive, temporaryFile) = await EnsureSeekableAsync(package, cancellationToken);

        Directory.CreateDirectory(skillRoot);

        var staging = Path.Combine(skillRoot, StagingPrefix + Guid.NewGuid().ToString("N"));

        try
        {
            var error = SkillPackageReader.ExtractTo(archive, staging);

            if (error is not null)
            {
                _logger.LogWarning(
                    "Скилл {SkillName} ({SkillId}) не удалось развернуть: {Error}",
                    skill.Name,
                    skill.Id,
                    error);

                return null;
            }

            try
            {
                // Папка появляется на своём месте целиком и разом: до этого момента агент
                // видит либо готовый скилл, либо ничего, но не полураспакованный.
                Directory.Move(staging, target);
            }
            catch (IOException) when (Directory.Exists(target))
            {
                // Другой процесс успел развернуть тот же скилл — его копия ничем не хуже нашей.
            }

            Touch(target);

            _logger.LogInformation(
                "Скилл {SkillName} ({SkillId}) развёрнут в {Directory}.",
                skill.Name,
                skill.Id,
                target);

            return target;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Скилл {SkillName} ({SkillId}) не удалось развернуть.", skill.Name, skill.Id);
            return null;
        }
        finally
        {
            if (archive != package)
                await archive.DisposeAsync();

            TryDeleteDirectory(staging);
            TryDeleteFile(temporaryFile);
        }
    }

    private static async Task<(Stream Archive, string? TemporaryFile)> EnsureSeekableAsync(
        Stream package,
        CancellationToken cancellationToken)
    {
        if (package.CanSeek)
            return (package, null);

        var path = Path.Combine(Path.GetTempPath(), "skill-" + Guid.NewGuid().ToString("N") + ".zip");
        var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        await package.CopyToAsync(file, cancellationToken);
        file.Seek(0, SeekOrigin.Begin);

        return (file, path);
    }

    /// <summary>
    /// Отмечает, что папкой пользовались. Маркер лежит рядом с папкой, а не внутри: любой
    /// лишний файл внутри скилла модель увидела бы как его ресурс.
    /// </summary>
    private void Touch(string target)
    {
        try
        {
            var marker = target + ".used";

            if (File.Exists(marker))
                File.SetLastWriteTimeUtc(marker, DateTime.UtcNow);
            else
                File.WriteAllBytes(marker, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Отметка о последнем использовании — вещь служебная, ради неё ничего не ломаем.
            _logger.LogDebug(exception, "Не удалось обновить отметку использования для {Directory}.", target);
        }
    }

    private static string ResolveRoot(string? configured)
    {
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "agentframework", "skills")
            : configured;

        // Папку не создаём: если скиллами никто не пользуется, её и быть не должно.
        // Создастся при первой распаковке.
        return Path.GetFullPath(path);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Мусор в кэше уберёт чистильщик.
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (path is null)
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
            gate.Dispose();

        _locks.Clear();
    }
}
