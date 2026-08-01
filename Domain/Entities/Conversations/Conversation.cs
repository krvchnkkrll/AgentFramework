using Domain.Common;
using Domain.Entities.Conversations.Parameters;
using Domain.Entities.Messages;
using Domain.Entities.Messages.Parameters;

namespace Domain.Entities.Conversations;

public sealed class Conversation : Entity
{
    private readonly List<Message> _messages = [];

    public Guid UserId { get; private init; }
    public string Title { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsPinned { get; private set; }
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation()
    {
        Title = null!;
    }

    private Conversation(Guid id, Guid userId, string title, DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        Title = title;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Conversation Create(CreateConversationParameter parameter)
    {
        if (parameter.UserId == Guid.Empty)
            throw new ArgumentException("Conversation must belong to a user.", nameof(parameter));

        var now = DateTimeOffset.UtcNow;
        return new Conversation(Guid.CreateVersion7(), parameter.UserId, NormalizeTitle(parameter.Title), now);
    }

    public void Rename(string title)
    {
        Title = NormalizeTitle(title);
        Touch();
    }

    public void Pin() => SetPinned(true);

    public void Unpin() => SetPinned(false);

    public Message AddMessage(AddMessageParameter parameter)
    {
        var message = Message.Create(new CreateMessageParameter
        {
            ConversationId = Id,
            RoleEnum = parameter.RoleEnum,
            Text = parameter.Text,
        });

        _messages.Add(message);
        Touch();

        return message;
    }

    private void SetPinned(bool isPinned)
    {
        if (IsPinned == isPinned)
            return;

        IsPinned = isPinned;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Conversation title cannot be empty.", nameof(title));

        var trimmed = title.Trim();

        return trimmed;
    }
}
