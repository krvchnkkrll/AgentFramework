namespace Application.Contracts.Features.Chats.Queries.GetChat;

public sealed record GetChatRequest
{
    public required Guid Id { get; init; }
}