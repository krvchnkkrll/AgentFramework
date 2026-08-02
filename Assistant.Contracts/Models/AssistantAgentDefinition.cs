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
    /// Имена скиллов, доступных агенту. Пусто — скиллов у агента нет вообще
    /// (а не «все», как можно было бы подумать: пустой список — это осознанный выбор в конструкторе).
    /// </summary>
    public IReadOnlyList<string> Skills { get; init; } = [];

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

/// <summary>Скилл, доступный для выбора в конструкторе агента.</summary>
public sealed record AssistantSkillInfo
{
    public required string Name { get; init; }

    public required string Description { get; init; }
}
