namespace Assistant.Contracts.Models;

/// <summary>
/// Информация об одном вызове инструмента: что агент позвал, с какими аргументами и что получил в ответ.
/// </summary>
public sealed record AssistantToolCall
{
    public required string CallId { get; init; }

    public required string Name { get; init; }

    public string? Arguments { get; init; }

    public string? Result { get; init; }

    public string? Error { get; init; }
}
