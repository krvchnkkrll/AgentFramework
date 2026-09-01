namespace Domain.Entities.Skills.Parameters;

/// <summary>Заведение нового скилла.</summary>
public sealed record CreateSkillParameter
{
    /// <summary>Владелец. null — общий скилл, видимый всем.</summary>
    public Guid? UserId { get; init; }

    public required SkillContentParameter Content { get; init; }
}

/// <summary>
/// Содержимое загруженного архива. Имя и описание сюда приходят уже вычитанными из
/// frontmatter SKILL.md, а сам файл — уже уложенным в хранилище: домен ни архивы не
/// разбирает, ни файлы не пишет.
/// </summary>
public sealed record SkillContentParameter
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Ключ архива в файловом хранилище.</summary>
    public required Guid FileId { get; init; }

    /// <summary>SHA-256 архива в hex.</summary>
    public required string ContentHash { get; init; }

    public required long SizeBytes { get; init; }

    public bool HasScripts { get; init; }
}
