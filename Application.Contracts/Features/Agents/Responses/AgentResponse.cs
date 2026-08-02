using Domain.Enums;

namespace Application.Contracts.Features.Agents.Responses;

/// <summary>Агент, собранный пользователем в конструкторе.</summary>
public sealed record AgentResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    /// <summary>Системный промпт в Markdown.</summary>
    public string? Instructions { get; init; }

    public required IReadOnlyCollection<string> Skills { get; init; }

    public required float Temperature { get; init; }

    public required float TopP { get; init; }

    public required int TopK { get; init; }

    public required int MaxOutputTokens { get; init; }

    public required float FrequencyPenalty { get; init; }

    public required float PresencePenalty { get; init; }

    public required ReasoningEffortEnum ReasoningEffortEnum { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Скилл, доступный для выбора в конструкторе.</summary>
public sealed record SkillResponse
{
    public required string Name { get; init; }

    public required string Description { get; init; }
}
