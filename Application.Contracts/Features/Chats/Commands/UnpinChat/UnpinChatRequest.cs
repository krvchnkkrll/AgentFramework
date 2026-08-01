namespace Application.Contracts.Features.Chats.Commands.UnpinChat;

public sealed record UnpinChatRequest
{
    public required Guid ChatId { get; init; }
}
