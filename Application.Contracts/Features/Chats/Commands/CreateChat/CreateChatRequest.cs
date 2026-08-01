namespace Application.Contracts.Features.Chats.Commands.CreateChat;

public sealed record CreateChatRequest
{
    public required string Title { get; init; }
}
