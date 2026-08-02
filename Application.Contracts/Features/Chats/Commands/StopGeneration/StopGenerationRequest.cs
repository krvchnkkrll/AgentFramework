namespace Application.Contracts.Features.Chats.Commands.StopGeneration;

public sealed record StopGenerationRequest
{
    public required Guid ChatId { get; init; }
}
