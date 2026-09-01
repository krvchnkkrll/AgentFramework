using Application.Contracts.Features.Agents;
using Application.Contracts.Features.Agents.Responses;
using Assistant.Contracts;
using Domain.Common;
using Domain.Entities.Agents;
using Domain.Entities.Skills;
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

file sealed class CreateAgentCommandHandler(
    ICurrentUserService currentUserService,
    IAgentRepository agentRepository,
    ISkillRepository skillRepository)
    : IRequestHandler<CreateAgentCommand, Result<AgentResponse>>
{
    public async Task<Result<AgentResponse>> Handle(CreateAgentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<AgentResponse>(userIdResult.Error);

        if (string.IsNullOrWhiteSpace(request.Body.Name))
            return Result.Failure<AgentResponse>(Error.Validation("Agent.NameRequired", "Укажите название агента."));

        var skillsResult = await SkillSelection.ResolveAsync(
            skillRepository,
            userIdResult.Value,
            request.Body.SkillIds,
            cancellationToken);

        if (skillsResult.IsFailure)
            return Result.Failure<AgentResponse>(skillsResult.Error);

        var agent = Agent.Create(request.Body.ToParameter(userIdResult.Value, skillsResult.Value));

        await agentRepository.AddAsync(agent, cancellationToken);
        await agentRepository.SaveChangesAsync(cancellationToken);

        return agent.ToResponse();
    }
}

file sealed class UpdateAgentCommandHandler(
    ICurrentUserService currentUserService,
    IAgentRepository agentRepository,
    ISkillRepository skillRepository,
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

        var skillsResult = await SkillSelection.ResolveAsync(
            skillRepository,
            userIdResult.Value,
            request.Body.SkillIds,
            cancellationToken);

        if (skillsResult.IsFailure)
            return Result.Failure<AgentResponse>(skillsResult.Error);

        agent.Update(request.Body.ToParameter(userIdResult.Value, skillsResult.Value));

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

/// <summary>
/// Разбор списка скиллов из формы конструктора.
///
/// Идентификаторы приходят от клиента, поэтому проверяются: подставив чужой id, забрать
/// чужой скилл нельзя — репозиторий отдаёт только доступные, а недостающие превращаются
/// в ошибку, а не молча пропадают из набора.
/// </summary>
file static class SkillSelection
{
    public static async Task<Result<IReadOnlyList<Skill>>> ResolveAsync(
        ISkillRepository skillRepository,
        Guid userId,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        if (skillIds.Count == 0)
            return Result.Success<IReadOnlyList<Skill>>([]);

        var requested = skillIds.Distinct().ToArray();
        var skills = await skillRepository.GetAvailableByIdsAsync(userId, requested, cancellationToken);

        if (skills.Count != requested.Length)
        {
            return Result.Failure<IReadOnlyList<Skill>>(
                Error.NotFound("Agent.SkillNotFound", "Часть выбранных скиллов не найдена."));
        }

        return Result.Success<IReadOnlyList<Skill>>([.. skills]);
    }
}
