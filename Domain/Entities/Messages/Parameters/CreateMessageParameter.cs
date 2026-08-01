using Domain.Enums;

namespace Domain.Entities.Messages.Parameters;

public readonly struct CreateMessageParameter
{
    public required Guid ConversationId { get; init; }
    public required MessageRoleEnum RoleEnum { get; init; }
    public required string Text { get; init; }
}
