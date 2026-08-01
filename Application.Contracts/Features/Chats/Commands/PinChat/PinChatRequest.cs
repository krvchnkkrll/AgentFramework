namespace Application.Contracts.Features.Chats.Commands.PinChat;

public sealed record PinChatRequest
{
    public required Guid ChatId { get; init; }
}
