using Domain.Common;
using Domain.Entities.Skills.Parameters;

namespace Domain.Entities.Skills;

/// <summary>
/// Скилл — папка с файлом SKILL.md и ресурсами рядом, упакованная в zip.
///
/// В базе лежит только карточка: имя, описание и ссылка на файл в хранилище. Сам архив
/// в базу не попадает — за него отвечает файловое хранилище, а здесь остаётся его
/// идентификатор (<see cref="FileId"/>).
///
/// Имя и описание — не самостоятельные поля, а копия frontmatter из SKILL.md. Их нельзя
/// править в интерфейсе: слой ассистента отбирает скиллы агента сравнением с
/// frontmatter.Name, и разъехавшееся имя означало бы, что выбранный скилл до модели
/// просто не доедет. Поэтому при загрузке архива они вычитываются из него и переписываются
/// целиком — база здесь индекс над файлом, а не второй источник правды.
/// </summary>
public sealed class Skill : Entity
{
    public const int MaxNameLength = 100;

    public const int MaxDescriptionLength = 1024;

    /// <summary>SHA-256 в hex — всегда ровно 64 символа.</summary>
    public const int ContentHashLength = 64;

    /// <summary>
    /// Владелец. null — общий скилл, доступный всем: такие заводятся не через интерфейс,
    /// а миграцией или руками в базе.
    /// </summary>
    public Guid? UserId { get; private init; }

    /// <summary>Имя из frontmatter. Уникально в пределах владельца.</summary>
    public string Name { get; private set; }

    /// <summary>Описание из frontmatter. Именно оно уезжает в системный промпт агента.</summary>
    public string Description { get; private set; }

    /// <summary>Ключ zip-архива в файловом хранилище.</summary>
    public Guid FileId { get; private set; }

    /// <summary>
    /// SHA-256 архива. Нужен не для проверки целостности, а как ключ кэша распаковки:
    /// содержимое поменялось — поменялся хэш — ассистент разворачивает скилл заново.
    /// </summary>
    public string ContentHash { get; private set; }

    public long SizeBytes { get; private set; }

    /// <summary>
    /// В архиве есть исполняемые файлы. Запускать их пользовательским скиллам всё равно
    /// нельзя, но знать об этом полезно: и чтобы показать предупреждение в интерфейсе,
    /// и чтобы при разборе инцидента было видно, что именно загружали.
    /// </summary>
    public bool HasScripts { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Меняется при загрузке нового содержимого.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    private Skill()
    {
        Name = null!;
        Description = null!;
        ContentHash = null!;
    }

    private Skill(Guid id, Guid? userId, DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        Name = null!;
        Description = null!;
        ContentHash = null!;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Skill Create(CreateSkillParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var skill = new Skill(Guid.CreateVersion7(), parameter.UserId, DateTimeOffset.UtcNow);
        skill.Apply(parameter.Content);

        return skill;
    }

    /// <summary>
    /// Загружает в скилл новую версию архива. Имя при этом может поменяться — это осознанно:
    /// правда лежит во frontmatter, а не в базе.
    /// </summary>
    public void UpdateContent(SkillContentParameter content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Apply(content);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Apply(SkillContentParameter content)
    {
        Name = NormalizeName(content.Name);
        Description = NormalizeDescription(content.Description);
        FileId = content.FileId != Guid.Empty
            ? content.FileId
            : throw new ArgumentException("Skill file id cannot be empty.", nameof(content));
        ContentHash = NormalizeHash(content.ContentHash);
        SizeBytes = content.SizeBytes >= 0
            ? content.SizeBytes
            : throw new ArgumentOutOfRangeException(nameof(content), "Skill size cannot be negative.");
        HasScripts = content.HasScripts;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty.", nameof(name));

        var trimmed = name.Trim();

        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Skill description cannot be empty.", nameof(description));

        var trimmed = description.Trim();

        return trimmed.Length > MaxDescriptionLength ? trimmed[..MaxDescriptionLength] : trimmed;
    }

    private static string NormalizeHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Trim().Length != ContentHashLength)
            throw new ArgumentException("Skill content hash must be a SHA-256 hex string.", nameof(hash));

        return hash.Trim().ToLowerInvariant();
    }
}
