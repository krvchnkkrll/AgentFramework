using Assistant.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assistant.Skills;

/// <summary>
/// Убирает из кэша скиллов то, чем давно не пользовались.
///
/// Без этого кэш растёт молча и навсегда: каждая новая версия каждого скилла — это ещё одна
/// папка, а старые никто не удаляет, потому что в момент загрузки новой версии старой может
/// пользоваться работающий прямо сейчас агент. Поэтому чистка идёт не по факту замены, а по
/// возрасту: папку сносим, только если к ней давно не обращались.
/// </summary>
internal sealed class SkillWorkspaceCleaner(
    SkillWorkspace workspace,
    IOptions<AssistantOptions> options,
    ILogger<SkillWorkspaceCleaner> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromHours(1);

    /// <summary>Недоделанные распаковки живут заметно меньше — это заведомо мусор.</summary>
    private static readonly TimeSpan StagingLifetime = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = TimeSpan.FromHours(Math.Max(1, options.Value.Skills.CacheLifetimeHours));

        using var timer = new PeriodicTimer(Period);

        // Первый проход — сразу на старте: после перезапуска в кэше вполне может лежать
        // мусор, оставшийся от прерванной работы.
        do
        {
            try
            {
                Clean(lifetime);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Не удалось почистить кэш скиллов.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void Clean(TimeSpan lifetime)
    {
        if (!Directory.Exists(workspace.Root))
            return;

        var now = DateTime.UtcNow;
        var removed = 0;

        foreach (var skillDirectory in Directory.EnumerateDirectories(workspace.Root))
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(skillDirectory))
            {
                var name = Path.GetFileName(versionDirectory);

                // Брошенная распаковка: её никто не «трогал» и не тронет.
                if (name.StartsWith(".staging-", StringComparison.Ordinal))
                {
                    if (now - Directory.GetCreationTimeUtc(versionDirectory) > StagingLifetime)
                        removed += Remove(versionDirectory) ? 1 : 0;

                    continue;
                }

                if (now - GetLastUsedUtc(versionDirectory) <= lifetime)
                    continue;

                if (Remove(versionDirectory))
                {
                    TryDeleteMarker(versionDirectory + ".used");
                    removed++;
                }
            }

            // Папка скилла, из которой всё вычистили, больше не нужна.
            TryRemoveEmpty(skillDirectory);
        }

        if (removed > 0)
            logger.LogInformation("Из кэша скиллов удалено папок: {Count}.", removed);
    }

    /// <summary>
    /// Когда папкой пользовались в последний раз. Маркер мог не появиться (например, диск
    /// был только для чтения) — тогда считаем по времени создания самой папки.
    /// </summary>
    private static DateTime GetLastUsedUtc(string versionDirectory)
    {
        var marker = versionDirectory + ".used";

        return File.Exists(marker)
            ? File.GetLastWriteTimeUtc(marker)
            : Directory.GetCreationTimeUtc(versionDirectory);
    }

    private bool Remove(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Кто-то держит файл открытым — попробуем в следующий заход.
            logger.LogDebug(exception, "Не удалось удалить {Directory} из кэша скиллов.", directory);
            return false;
        }
    }

    private static void TryDeleteMarker(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryRemoveEmpty(string directory)
    {
        try
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                return;

            Directory.Delete(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
