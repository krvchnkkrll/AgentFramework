using Domain.Enums;

namespace Domain.Entities.Conversations.Parameters;

public readonly struct AddMessageParameter
{
    /// <summary>
    /// Pre-allocated id — lets a caller correlate a message with events raised (e.g. over
    /// SignalR) before this call persists it. Null means "generate a new one".
    /// </summary>
    public Guid? Id { get; init; }

    public required MessageRoleEnum RoleEnum { get; init; }
    public required string Text { get; init; }
}
