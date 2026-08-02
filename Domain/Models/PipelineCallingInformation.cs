namespace Domain.Models;

public sealed class PipelineCallingInformation
{
    public required Guid Id { get; init; }
    public required string CallingName { get; init; }
    public required string? CallingInformation { get; init; }
}