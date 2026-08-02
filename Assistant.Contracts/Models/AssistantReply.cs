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
}
