namespace Application.Contracts.Features.Skills.Responses;

/// <summary>
/// Скилл в том виде, в каком его видит фронт: карточка из базы. Самого архива здесь нет —
/// он лежит в файловом хранилище и наружу не отдаётся.
/// </summary>
public sealed record SkillResponse
{
    public required Guid Id { get; init; }

    /// <summary>Имя из frontmatter SKILL.md. Редактировать его отдельно нельзя.</summary>
    public required string Name { get; init; }

    /// <summary>Описание из frontmatter — по нему модель решает, брать скилл в работу.</summary>
    public required string Description { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>В архиве есть скрипты. Запускать их пользовательским скиллам всё равно нельзя.</summary>
    public required bool HasScripts { get; init; }

    /// <summary>
    /// Скилл общий: заведён администратором и доступен всем. Такой нельзя ни перезалить,
    /// ни удалить из интерфейса.
    /// </summary>
    public required bool Shared { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
