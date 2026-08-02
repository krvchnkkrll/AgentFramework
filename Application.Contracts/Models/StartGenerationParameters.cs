namespace Application.Contracts.Models;

public readonly struct StartGenerationParameters
{
    public required Guid UserId { get; init; }
    public required Guid ConversationId { get; init; }
}