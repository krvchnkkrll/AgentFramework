using Domain.Enums;

namespace Domain.Entities.Agents.Parameters;

/// <summary>Всё, что задаёт пользователь в конструкторе агента.</summary>
public sealed record CreateAgentParameter
{
    public required Guid UserId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Эмодзи или один символ для аватарки.</summary>
    public string? Icon { get; init; }

    /// <summary>Системный промпт в Markdown. Пусто — возьмётся встроенный.</summary>
    public string? Instructions { get; init; }

    /// <summary>Имена скиллов, доступных этому агенту.</summary>
    public IReadOnlyList<string> Skills { get; init; } = [];

    public AgentGenerationParameter Generation { get; init; } = new();
}

/// <summary>Параметры генерации агента. Значения по умолчанию совпадают со встроенным агентом.</summary>
public sealed record AgentGenerationParameter
{
    public float Temperature { get; init; } = 0.7f;

    public float TopP { get; init; } = 0.95f;

    public int TopK { get; init; } = 40;

    public int MaxOutputTokens { get; init; } = 8192;

    public float FrequencyPenalty { get; init; }

    public float PresencePenalty { get; init; }

    public ReasoningEffortEnum ReasoningEffortEnum { get; init; } = ReasoningEffortEnum.None;
}
