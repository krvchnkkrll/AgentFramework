using Application.Contracts.Features.Agents;
using Application.Contracts.Features.Agents.Responses;
using Assistant.Contracts;
using Domain.Common;
using Domain.Entities.Agents;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Agents;

/// <summary>Агенты текущего пользователя. Чужих не видно и не отдаётся.</summary>
file sealed class GetAgentsQueryHandler(
    ICurrentUserService currentUserService,
    IAgentRepository agentRepository)
    : IRequestHandler<GetAgentsQuery, Result<IReadOnlyCollection<AgentResponse>>>
{
    public async Task<Result<IReadOnlyCollection<AgentResponse>>> Handle(
        GetAgentsQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<IReadOnlyCollection<AgentResponse>>(userIdResult.Error);

        var agents = await agentRepository.GetByUserIdAsync(userIdResult.Value, cancellationToken);

        return Result.Success<IReadOnlyCollection<AgentResponse>>(
            [.. agents.Select(agent => agent.ToResponse())]);
    }
}

/// <summary>
/// Скиллы, из которых можно выбирать в конструкторе. Список берётся не из БД, а с диска —
/// скиллы кладут в папку, а не заводят в интерфейсе.
/// </summary>
file sealed class GetSkillsQueryHandler(IAssistantAgent assistantAgent)
    : IRequestHandler<GetSkillsQuery, Result<IReadOnlyCollection<SkillResponse>>>
{
    public async Task<Result<IReadOnlyCollection<SkillResponse>>> Handle(
        GetSkillsQuery request,
        CancellationToken cancellationToken)
    {
        var skills = await assistantAgent.GetAvailableSkillsAsync(cancellationToken);

        return Result.Success<IReadOnlyCollection<SkillResponse>>(
        [
            .. skills.Select(skill => new SkillResponse
            {
                Name = skill.Name,
                Description = skill.Description,
            }),
        ]);
    }
}

file sealed class CreateAgentCommandHandler(
    ICurrentUserService currentUserService,
    IAgentRepository agentRepository)
    : IRequestHandler<CreateAgentCommand, Result<AgentResponse>>
{
    public async Task<Result<AgentResponse>> Handle(CreateAgentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<AgentResponse>(userIdResult.Error);

        if (string.IsNullOrWhiteSpace(request.Body.Name))
            return Result.Failure<AgentResponse>(Error.Validation("Agent.NameRequired", "Укажите название агента."));

        var agent = Agent.Create(request.Body.ToParameter(userIdResult.Value));

        await agentRepository.AddAsync(agent, cancellationToken);
        await agentRepository.SaveChangesAsync(cancellationToken);

        return agent.ToResponse();
    }
}

file sealed class UpdateAgentCommandHandler(
    ICurrentUserService currentUserService,
    IAgentRepository agentRepository,
    IAssistantAgent assistantAgent)
    : IRequestHandler<UpdateAgentCommand, Result<AgentResponse>>
{
    public async Task<Result<AgentResponse>> Handle(UpdateAgentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<AgentResponse>(userIdResult.Error);

        if (string.IsNullOrWhiteSpace(request.Body.Name))
            return Result.Failure<AgentResponse>(Error.Validation("Agent.NameRequired", "Укажите название агента."));

        var agent = await agentRepository.GetByIdAsync(request.AgentId, cancellationToken);

        if (agent is null || agent.UserId != userIdResult.Value)
            return Result.Failure<AgentResponse>(Error.NotFound("Agent.NotFound", "Агент не найден."));

        agent.Update(request.Body.ToParameter(userIdResult.Value));

        await agentRepository.SaveChangesAsync(cancellationToken);

        // Собранный в память агент уже не соответствует настройкам — выкидываем, чтобы
        // следующее сообщение пересобрало его с новым промптом и скиллами.
        assistantAgent.EvictAgent(agent.Id);

        return agent.ToResponse();
    }
}

file sealed class DeleteAgentCommandHandler(
    ICurrentUserService currentUserService,
    IAgentRepository agentRepository,
    IAssistantAgent assistantAgent)
    : IRequestHandler<DeleteAgentCommand, Result>
{
    public async Task<Result> Handle(DeleteAgentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure(userIdResult.Error);

        var agent = await agentRepository.GetByIdAsync(request.AgentId, cancellationToken);

        if (agent is null || agent.UserId != userIdResult.Value)
            return Result.Failure(Error.NotFound("Agent.NotFound", "Агент не найден."));

        agentRepository.Remove(agent);

        // Чаты этого агента не удаляются — внешний ключ настроен на SetNull, и они просто
        // возвращаются к встроенному агенту.
        await agentRepository.SaveChangesAsync(cancellationToken);

        assistantAgent.EvictAgent(agent.Id);

        return Result.Success();
    }
}
