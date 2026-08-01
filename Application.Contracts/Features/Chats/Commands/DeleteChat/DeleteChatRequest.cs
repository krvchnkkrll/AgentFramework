namespace Application.Contracts.Features.Chats.Commands.DeleteChat;

public sealed record DeleteChatRequest
{
    public required Guid ChatId { get; init; }
}
