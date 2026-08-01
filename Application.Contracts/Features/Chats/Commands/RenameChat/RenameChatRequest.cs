namespace Application.Contracts.Features.Chats.Commands.RenameChat;

public sealed record RenameChatRequest
{
    public required Guid ChatId { get; init; }
    public required string Title { get; init; }
}
