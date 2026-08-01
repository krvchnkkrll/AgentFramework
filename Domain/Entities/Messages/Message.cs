using Domain.Common;
using Domain.Entities.Messages.Parameters;
using Domain.Enums;

namespace Domain.Entities.Messages;

public sealed class Message : Entity
{
    public const int MaxTextLength = 32_000;

    public Guid ConversationId { get; private init; }
    public MessageRoleEnum RoleEnum { get; private init; }
    public string Text { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private Message()
    {
        Text = null!;
    }

    private Message(Guid id, Guid conversationId, MessageRoleEnum roleEnum, string text, DateTimeOffset createdAt)
        : base(id)
    {
        ConversationId = conversationId;
        RoleEnum = roleEnum;
        Text = text;
        CreatedAt = createdAt;
    }

    // Only Conversation (the aggregate root) may create a Message — keeps every
    // message attached to a conversation that actually owns it.
    internal static Message Create(CreateMessageParameter parameter)
    {
        if (parameter.Id == Guid.Empty)
            throw new ArgumentException("Message id cannot be empty.", nameof(parameter));

        if (parameter.ConversationId == Guid.Empty)
            throw new ArgumentException("Message must belong to a conversation.", nameof(parameter));

        return new Message(
            parameter.Id,
            parameter.ConversationId,
            parameter.RoleEnum,
            NormalizeText(parameter.Text),
            DateTimeOffset.UtcNow);
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Message text cannot be empty.", nameof(text));

        var trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
            throw new ArgumentException($"Message text cannot exceed {MaxTextLength} characters.", nameof(text));

        return trimmed;
    }
}
