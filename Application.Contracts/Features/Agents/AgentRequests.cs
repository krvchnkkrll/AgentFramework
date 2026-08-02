using Application.Contracts.Features.Agents.Responses;
using Domain.Common;
using Domain.Enums;
using MediatR;

namespace Application.Contracts.Features.Agents;

/// <summary>
/// Форма конструктора агента. Приходит целиком и на создание, и на изменение —
/// частичных обновлений здесь нет, пользователь всегда сохраняет форму как есть.
/// </summary>
public sealed record SaveAgentRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    /// <summary>Системный промпт в Markdown.</summary>
    public string? Instructions { get; init; }

    public IReadOnlyList<string> Skills { get; init; } = [];

    public float Temperature { get; init; } = 0.7f;

    public float TopP { get; init; } = 0.95f;

    public int TopK { get; init; } = 40;

    public int MaxOutputTokens { get; init; } = 8192;

    public float FrequencyPenalty { get; init; }

    public float PresencePenalty { get; init; }

    public ReasoningEffortEnum ReasoningEffortEnum { get; init; } = ReasoningEffortEnum.None;
}

public sealed record GetAgentsQuery : IRequest<Result<IReadOnlyCollection<AgentResponse>>>;

public sealed record GetSkillsQuery : IRequest<Result<IReadOnlyCollection<SkillResponse>>>;

public sealed record CreateAgentCommand(SaveAgentRequest Body) : IRequest<Result<AgentResponse>>;

public sealed record UpdateAgentCommand(Guid AgentId, SaveAgentRequest Body) : IRequest<Result<AgentResponse>>;

public sealed record DeleteAgentCommand(Guid AgentId) : IRequest<Result>;
