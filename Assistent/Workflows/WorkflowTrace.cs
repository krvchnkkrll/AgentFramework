using System.Collections.Concurrent;
using System.Diagnostics;
using Assistant.Contracts.Workflows;

namespace Assistant.Workflows;

/// <summary>
/// Секундомер прогона. Один экземпляр на один прогон, пишут в него из разных потоков —
/// подагенты в режиме Workflow работают параллельно, — поэтому очередь конкурентная.
///
/// Записываем не длительность, а пару «начало-конец» от старта прогона. Длительность из них
/// выводится, а обратно — нет: именно по пересечению интервалов видно, шли этапы параллельно
/// или встали в очередь. Ради этого всё и затевалось.
/// </summary>
internal sealed class WorkflowTrace
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ConcurrentQueue<DocumentWorkflowStage> _stages = new();

    public double ElapsedMs => _clock.Elapsed.TotalMilliseconds;

    public IReadOnlyList<DocumentWorkflowStage> Stages =>
        [.. _stages.OrderBy(stage => stage.StartedAtMs)];

    /// <summary>Замеряет операцию целиком, включая падение: упавший этап тоже попадает в отчёт.</summary>
    public async Task<T> MeasureAsync<T>(
        string name,
        string kind,
        Func<CancellationToken, Task<T>> operation,
        Func<T, string?>? detail = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = ElapsedMs;

        try
        {
            var result = await operation(cancellationToken);

            Add(name, kind, startedAt, detail?.Invoke(result));

            return result;
        }
        catch (Exception exception)
        {
            Add(name, kind, startedAt, detail: null, error: exception.Message);
            throw;
        }
    }

    public void Add(string name, string kind, double startedAtMs, string? detail = null, string? error = null) =>
        _stages.Enqueue(new DocumentWorkflowStage
        {
            Name = name,
            Kind = kind,
            StartedAtMs = Math.Round(startedAtMs, 1),
            FinishedAtMs = Math.Round(ElapsedMs, 1),
            Detail = detail,
            Error = error,
        });

    /// <summary>
    /// Сколько времени сэкономила параллельность на этапах указанного вида: сумма длительностей
    /// минус длина их объединённого интервала. Ноль — этапы не пересекались, то есть шли по очереди.
    /// </summary>
    public double ParallelSaving(string kind)
    {
        var intervals = _stages
            .Where(stage => stage.Kind == kind)
            .OrderBy(stage => stage.StartedAtMs)
            .ToList();

        if (intervals.Count < 2)
            return 0;

        var total = intervals.Sum(stage => stage.DurationMs);
        var covered = 0d;
        var currentStart = intervals[0].StartedAtMs;
        var currentEnd = intervals[0].FinishedAtMs;

        foreach (var interval in intervals.Skip(1))
        {
            if (interval.StartedAtMs > currentEnd)
            {
                covered += currentEnd - currentStart;
                currentStart = interval.StartedAtMs;
                currentEnd = interval.FinishedAtMs;
                continue;
            }

            currentEnd = Math.Max(currentEnd, interval.FinishedAtMs);
        }

        covered += currentEnd - currentStart;

        return Math.Round(Math.Max(0, total - covered), 1);
    }

    public double TotalOf(string kind) =>
        Math.Round(_stages.Where(stage => stage.Kind == kind).Sum(stage => stage.DurationMs), 1);

    public int CountOf(string kind) => _stages.Count(stage => stage.Kind == kind);
}

/// <summary>Виды этапов. Строками, потому что они уезжают в JSON и в отчёт как есть.</summary>
internal static class WorkflowStageKinds
{
    /// <summary>Обращение к сервису-заглушке — та самая пятисекундная задержка.</summary>
    public const string Api = "api";

    /// <summary>Один запрос к модели.</summary>
    public const string Model = "model";

    /// <summary>Полный прогон одного агента: несколько запросов к модели плюс его инструменты.</summary>
    public const string Agent = "agent";
}
