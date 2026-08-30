namespace Assistant.Contracts.Workflows;

/// <summary>
/// Способ, которым главный агент добывает данные у двух вспомогательных.
/// От него зависит и время прогона, и количество обращений к модели.
/// </summary>
public enum DocumentWorkflowModeEnum
{
    /// <summary>
    /// Без модели вообще: оба «внешних API» дёргаются напрямую и параллельно, документ
    /// собирается шаблоном. Нижняя граница времени — быстрее физически не будет.
    /// </summary>
    DirectApi,

    /// <summary>
    /// Классическая мультиагентность: подагенты отданы главному агенту как инструменты,
    /// и он сам решает, кого и когда позвать. Параллельность не гарантирована — она
    /// случается ровно тогда, когда модель выдаёт оба вызова за один ход.
    /// </summary>
    AgentTools,

    /// <summary>
    /// Граф Microsoft.Agents.AI.Workflows: fan-out на двух подагентов, fan-in барьером
    /// в агента-составителя. Параллельность здесь заложена в топологию, а не в решение модели.
    /// </summary>
    Workflow,
}

/// <summary>Запрос на подготовку документа. Всё, что за ним стоит, — заглушки.</summary>
public sealed record DocumentWorkflowRequest
{
    /// <summary>Тип документа: служебная записка, приказ, доверенность, договор.</summary>
    public required string DocumentType { get; init; }

    /// <summary>Фамилии сотрудников, которых надо найти в оргструктуре.</summary>
    public required IReadOnlyList<string> EmployeeNames { get; init; }

    /// <summary>О чём документ. Уезжает в промпт составителя.</summary>
    public string? Subject { get; init; }
}

/// <summary>
/// Один замеренный этап прогона. Времена — от старта прогона, а не от эпохи: сравнивать
/// нужно этапы между собой, и по пересечению интервалов сразу видно, шли они параллельно или нет.
/// </summary>
public sealed record DocumentWorkflowStage
{
    /// <summary>Что мерили: вызов API, обращение к модели, работа подагента.</summary>
    public required string Name { get; init; }

    /// <summary>Категория этапа: api, model, agent, workflow. Нужна для группировки в отчёте.</summary>
    public required string Kind { get; init; }

    public required double StartedAtMs { get; init; }

    public required double FinishedAtMs { get; init; }

    public double DurationMs => FinishedAtMs - StartedAtMs;

    /// <summary>Короткая подробность: имя инструмента, размер ответа, тип документа.</summary>
    public string? Detail { get; init; }

    /// <summary>Заполняется, если этап упал. Прогон при этом не прерывается.</summary>
    public string? Error { get; init; }
}

/// <summary>Результат одного прогона в одном режиме.</summary>
public sealed record DocumentWorkflowResult
{
    public required DocumentWorkflowModeEnum Mode { get; init; }

    /// <summary>Общее время прогона по стенным часам.</summary>
    public required double TotalMs { get; init; }

    /// <summary>Сколько раз сходили в модель. Главный источник времени во всех режимах с LLM.</summary>
    public required int ModelCalls { get; init; }

    /// <summary>Суммарное время обращений к модели.</summary>
    public required double ModelMs { get; init; }

    /// <summary>Суммарное время обращений к «внешним API» (те самые задержки по 5 секунд).</summary>
    public required double ApiMs { get; init; }

    /// <summary>
    /// Сколько сэкономила параллельность на этапах API: сумма длительностей минус длина
    /// их объединённого интервала. Ноль — значит всё шло по очереди.
    /// </summary>
    public required double ApiParallelSavingMs { get; init; }

    public required IReadOnlyList<DocumentWorkflowStage> Stages { get; init; }

    /// <summary>Готовый текст документа — то, ради чего всё затевалось.</summary>
    public required string Text { get; init; }

    /// <summary>Заполняется, если прогон упал целиком.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Стенд для замера мультиагентного сценария «подготовь документ».
///
/// Сценарий один и тот же во всех режимах: найти сотрудников в оргструктуре, взять шаблон
/// документа в ЭДО, собрать из этого текст. Оба «внешних сервиса» — заглушки с искусственной
/// задержкой, чтобы было что распараллеливать.
/// </summary>
public interface IDocumentWorkflow
{
    Task<DocumentWorkflowResult> RunAsync(
        DocumentWorkflowRequest request,
        DocumentWorkflowModeEnum mode,
        CancellationToken cancellationToken = default);

    /// <summary>Схема графа в формате mermaid — для режима <see cref="DocumentWorkflowModeEnum.Workflow"/>.</summary>
    string GetDiagram();
}
