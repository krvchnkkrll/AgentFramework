namespace Domain.Entities.Conversations.Parameters;

public readonly struct CreateConversationParameter
{
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
}
