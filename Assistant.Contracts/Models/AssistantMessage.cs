namespace Assistant.Contracts.Models;

/// <summary>
/// Роль автора сообщения в истории диалога.
/// </summary>
public enum AssistantRoleEnum
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// Одно сообщение истории, которое отдаётся агенту при восстановлении сессии.
/// Это транспортный тип ассистента — он намеренно не знает про сущности Domain.
/// </summary>
public sealed record AssistantMessage
{
    public required AssistantRoleEnum RoleEnum { get; init; }

    public required string Text { get; init; }
}
