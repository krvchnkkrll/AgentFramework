namespace Application.Contracts.Features.Chats.Responses;

/// <summary>
/// Вызов инструмента агентом. Живёт только внутри одной генерации: в БД не сохраняется,
/// нужен, чтобы пользователь видел, чем занят агент, пока текста ещё нет.
/// </summary>
public sealed record ToolCallResponse
{
    /// <summary>Наш идентификатор вызова. Модель присылает свой (CallId), но наружу отдаём этот.</summary>
    public required Guid Id { get; init; }

    /// <summary>Имя инструмента, например get_current_time или load_skill.</summary>
    public required string Name { get; init; }

    /// <summary>Аргументы вызова в виде JSON. Может отсутствовать, если инструмент без параметров.</summary>
    public string? Arguments { get; init; }
}
