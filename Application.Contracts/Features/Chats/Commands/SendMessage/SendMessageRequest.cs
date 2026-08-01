namespace Application.Contracts.Features.Chats.Commands.SendMessage;

public sealed record SendMessageRequest
{
    public required Guid ChatId { get; init; }
    public required string Text { get; init; }
}
