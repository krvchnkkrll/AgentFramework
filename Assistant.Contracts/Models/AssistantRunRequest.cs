namespace Assistant.Contracts.Models;

/// <summary>
/// Всё, что нужно агенту для одного прогона. Отдельный тип, а не куча параметров, потому что
/// набор входа будет расти: сюда же поедут вложения, выбор агента и ответы на запросы подтверждения.
/// </summary>
public sealed record AssistantRunRequest
{
    /// <summary>Новое сообщение пользователя.</summary>
    public required string UserText { get; init; }

    /// <summary>
    /// История переписки из БД без последнего сообщения пользователя.
    /// Используется, только если <see cref="SessionState"/> пустой или битый.
    /// </summary>
    public IReadOnlyCollection<AssistantMessage> History { get; init; } = [];

    /// <summary>
    /// Сохранённое состояние сессии агента (то, что вернул прошлый прогон событием
    /// <see cref="AssistantUpdateKindEnum.SessionState"/>). Внутри — уже сжатая история,
    /// todo-лист, текущий режим и выданные подтверждения.
    /// Пусто — сессия соберётся заново из <see cref="History"/>.
    /// </summary>
    public string? SessionState { get; init; }

    /// <summary>
    /// Агент, который отвечает. Пусто — отвечает встроенный агент из конфигурации приложения.
    /// </summary>
    public AssistantAgentDefinition? Agent { get; init; }

    /// <summary>Пользователь, от имени которого идёт разговор. Уезжает в состояние сессии.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Чат, в котором идёт разговор. Уезжает в состояние сессии.</summary>
    public Guid? ConversationId { get; init; }
}
