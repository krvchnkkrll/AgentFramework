namespace Application.Contracts.Features.Chats.Commands.CreateChat;

public sealed record CreateChatRequest
{
    public required string Title { get; init; }

    /// <summary>
    /// Агент чата. Пусто — отвечает встроенный агент, именно так создаётся обычный новый чат.
    /// </summary>
    public Guid? AgentId { get; init; }
}
