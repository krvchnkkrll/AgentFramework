using Application.Contracts.Features.Agents;
using Application.Contracts.Features.Agents.Responses;
using Assistant.Contracts.Models;
using Domain.Entities.Agents;
using Domain.Entities.Agents.Parameters;
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
        Skills = agent.Skills,
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

    public static CreateAgentParameter ToParameter(this SaveAgentRequest request, Guid userId) => new()
    {
        UserId = userId,
        Name = request.Name,
        Description = request.Description,
        Icon = request.Icon,
        Instructions = request.Instructions,
        Skills = request.Skills,
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
        Skills = [.. agent.Skills],
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
