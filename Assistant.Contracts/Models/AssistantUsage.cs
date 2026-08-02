namespace Assistant.Contracts.Models;

/// <summary>
/// Потребление токенов за один запуск агента. Модель может не отдавать часть значений — тогда они null.
/// </summary>
public sealed record AssistantUsage
{
    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? TotalTokens { get; init; }
}
