namespace Assistant.Contracts.Models;

/// <summary>
/// Настройки агента, собранного пользователем в конструкторе. Слой Application передаёт это
/// в ассистента, а тот собирает по ним настоящий агент фреймворка.
///
/// Отдельный тип, а не сущность Domain: ассистент про Domain не знает и знать не должен.
/// </summary>
public sealed record AssistantAgentDefinition
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Системный промпт в Markdown. Пусто — берётся встроенный.</summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Скиллы, доступные агенту. Пусто — скиллов у агента нет вообще (а не «все», как можно
    /// было бы подумать: пустой список — это осознанный выбор в конструкторе).
    /// </summary>
    public IReadOnlyList<AssistantSkillReference> Skills { get; init; } = [];

    /// <summary>
    /// Отметка последнего изменения. По ней ассистент понимает, что собранный в память агент
    /// устарел, и пересобирает его — иначе правки в конструкторе не доезжали бы до модели
    /// до перезапуска приложения.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    public float Temperature { get; init; } = 0.7f;

    public float TopP { get; init; } = 0.95f;

    public int TopK { get; init; } = 40;

    public int MaxOutputTokens { get; init; } = 8192;

    public float FrequencyPenalty { get; init; }

    public float PresencePenalty { get; init; }

    public AssistantReasoningEffortEnum ReasoningEffortEnum { get; init; } = AssistantReasoningEffortEnum.None;
}

/// <summary>Насколько усердно модель «думает» перед ответом.</summary>
public enum AssistantReasoningEffortEnum
{
    /// <summary>Параметр не отправляется — как решит сервер.</summary>
    Default,

    /// <summary>Рассуждения выключены.</summary>
    None,

    Low,
    Medium,
    High,
}

/// <summary>
/// Ссылка на скилл, который надо дать агенту.
///
/// Содержимого здесь нет — только чем его найти: архив лежит в файловом хранилище, и слой
/// ассистента разворачивает его в кэш сам, когда собирает агента.
/// </summary>
public sealed record AssistantSkillReference
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Имя из frontmatter. Провайдер скиллов отбирает файлы именно по нему, поэтому оно
    /// обязано совпадать с тем, что лежит внутри SKILL.md.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Ключ архива в файловом хранилище.</summary>
    public required Guid FileId { get; init; }

    /// <summary>
    /// SHA-256 архива. Входит в путь распаковки, поэтому новая версия скилла разворачивается
    /// в новую папку, а старая остаётся жить, пока её кто-то использует.
    /// </summary>
    public required string ContentHash { get; init; }
}
