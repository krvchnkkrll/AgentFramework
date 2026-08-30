namespace Application.Contracts.Features.Chats.Commands.SendMessage;

public sealed record SendMessageRequest
{
    public required Guid ChatId { get; init; }
    public required string Text { get; init; }

    /// <summary>
    /// Ранее загруженные документы. Именно здесь они привязываются к чату: файл грузят
    /// до отправки, когда чата может ещё не существовать.
    /// </summary>
    public IReadOnlyList<Guid> AttachmentIds { get; init; } = [];
}
