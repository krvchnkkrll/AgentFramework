namespace Assistant.Contracts.Models;

/// <summary>
/// Результат нестримингового запуска агента.
/// </summary>
public sealed record AssistantReply
{
    public required string Text { get; init; }

    public required IReadOnlyList<AssistantToolCall> ToolCalls { get; init; }

    public AssistantUsage? Usage { get; init; }

    public string? FinishReason { get; init; }

    public string? ResponseId { get; init; }

    /// <summary>
    /// Состояние сессии агента после прогона. Сохраняется рядом с чатом и возвращается
    /// в следующем запросе — см. <see cref="AssistantRunRequest.SessionState"/>.
    /// </summary>
    public string? SessionState { get; init; }
}
