namespace Domain.Models;

public sealed class PipelineCallingInformation
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Идентификатор вызова, который прислала модель. По нему приходит результат инструмента,
    /// поэтому он нужен, чтобы связать «начали вызывать» и «закончили».
    /// </summary>
    public required string CallId { get; init; }

    public required string CallingName { get; init; }

    public required string? CallingInformation { get; init; }
}
