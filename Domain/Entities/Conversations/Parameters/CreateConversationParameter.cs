namespace Domain.Entities.Conversations.Parameters;

public readonly struct CreateConversationParameter
{
    public required Guid UserId { get; init; }
    public required string Title { get; init; }

    /// <summary>Агент чата. Пусто — отвечает встроенный агент из конфигурации приложения.</summary>
    public Guid? AgentId { get; init; }
}
