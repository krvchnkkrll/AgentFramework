using Application.Contracts.Features.Agents;
using Application.Contracts.Features.Agents.Responses;
using Application.Features.Skills;
using Assistant.Contracts.Models;
using Domain.Entities.Agents;
using Domain.Entities.Agents.Parameters;
using Domain.Entities.Skills;
using Domain.Enums;

namespace Application.Features.Agents;

internal static class AgentMappingExtensions
{
    public static AgentResponse ToResponse(this Agent agent) => new()
    {
        Id = agent.Id,
        Name = agent.Name,
        Description = agent.Description,
        Icon = agent.Icon,
        Instructions = agent.Instructions,
        Skills = [.. agent.Skills.OrderBy(skill => skill.Name).Select(skill => skill.ToResponse())],
        Temperature = agent.Temperature,
        TopP = agent.TopP,
        TopK = agent.TopK,
        MaxOutputTokens = agent.MaxOutputTokens,
        FrequencyPenalty = agent.FrequencyPenalty,
        PresencePenalty = agent.PresencePenalty,
        ReasoningEffortEnum = agent.ReasoningEffortEnum,
        CreatedAt = agent.CreatedAt,
        UpdatedAt = agent.UpdatedAt,
    };

    /// <param name="skills">
    /// Уже загруженные и проверенные скиллы. Домену их проверять нечем, поэтому право
    /// на каждый из них подтверждает обработчик команды.
    /// </param>
    public static CreateAgentParameter ToParameter(
        this SaveAgentRequest request,
        Guid userId,
        IReadOnlyList<Skill> skills) => new()
    {
        UserId = userId,
        Name = request.Name,
        Description = request.Description,
        Icon = request.Icon,
        Instructions = request.Instructions,
        Skills = skills,
        Generation = new AgentGenerationParameter
        {
            Temperature = request.Temperature,
            TopP = request.TopP,
            TopK = request.TopK,
            MaxOutputTokens = request.MaxOutputTokens,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            ReasoningEffortEnum = request.ReasoningEffortEnum,
        },
    };

    /// <summary>Переводит сущность в то, что понимает слой ассистента.</summary>
    public static AssistantAgentDefinition ToDefinition(this Agent agent) => new()
    {
        Id = agent.Id,
        Name = agent.Name,
        Description = agent.Description,
        Instructions = agent.Instructions,
        Skills = [.. agent.Skills.Select(skill => skill.ToReference())],
        UpdatedAt = agent.UpdatedAt,
        Temperature = agent.Temperature,
        TopP = agent.TopP,
        TopK = agent.TopK,
        MaxOutputTokens = agent.MaxOutputTokens,
        FrequencyPenalty = agent.FrequencyPenalty,
        PresencePenalty = agent.PresencePenalty,
        ReasoningEffortEnum = agent.ReasoningEffortEnum switch
        {
            ReasoningEffortEnum.Default => AssistantReasoningEffortEnum.Default,
            ReasoningEffortEnum.None => AssistantReasoningEffortEnum.None,
            ReasoningEffortEnum.Low => AssistantReasoningEffortEnum.Low,
            ReasoningEffortEnum.Medium => AssistantReasoningEffortEnum.Medium,
            ReasoningEffortEnum.High => AssistantReasoningEffortEnum.High,
            _ => AssistantReasoningEffortEnum.Default,
        },
    };
}
