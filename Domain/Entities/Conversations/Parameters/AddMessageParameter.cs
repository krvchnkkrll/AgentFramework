using Domain.Enums;

namespace Domain.Entities.Conversations.Parameters;

public readonly struct AddMessageParameter
{
    public required MessageRoleEnum RoleEnum { get; init; }
    public required string Text { get; init; }
}
