namespace Application.Contracts.Features.Chats.Queries.GetChatMessages;

public sealed record GetChatMessagesRequest
{
    public required Guid ChatId { get; init; }
}
