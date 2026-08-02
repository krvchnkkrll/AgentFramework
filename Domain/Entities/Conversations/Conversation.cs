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
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Агент, который отвечает в этом чате. Пусто — отвечает встроенный агент из конфигурации
    /// приложения, именно его получает любой новый чат.
    /// </summary>
    public Guid? AgentId { get; private set; }

    /// <summary>
    /// Состояние сессии агента в виде JSON: сжатая история, todo-лист, текущий режим и выданные
    /// подтверждения инструментов. Это «взгляд модели» на разговор, а не сама переписка —
    /// переписка лежит в <see cref="Messages"/> и остаётся источником правды для пользователя.
    /// </summary>
    public string? AgentState { get; private set; }


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

        return new Conversation(Guid.CreateVersion7(), parameter.UserId, NormalizeTitle(parameter.Title), now)
        {
            AgentId = parameter.AgentId,
        };
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
            Id = parameter.Id ?? Guid.CreateVersion7(),
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
    
    public void ResetError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    public void SetError(string error)
    {
        HasError = true;
        ErrorMessage = error;
    }

    /// <summary>
    /// Назначает чату агента. Состояние сессии при этом сбрасывается: в нём лежит контекст,
    /// собранный прошлым агентом — с его промптом, скиллами и сжатой под него историей.
    /// Подсовывать это новому агенту нельзя, он соберёт контекст заново из переписки.
    /// </summary>
    public void AssignAgent(Guid? agentId)
    {
        if (AgentId == agentId)
            return;

        AgentId = agentId;
        AgentState = null;
        Touch();
    }

    /// <summary>Сохраняет состояние сессии агента после очередного прогона.</summary>
    public void SaveAgentState(string? state)
    {
        AgentState = string.IsNullOrWhiteSpace(state) ? null : state;
    }

    /// <summary>
    /// Забывает состояние сессии. Нужно, когда переписку правят в обход агента — тогда
    /// сессию проще собрать заново из истории, чем чинить рассинхрон.
    /// </summary>
    public void ResetAgentState()
    {
        AgentState = null;
    }
}
