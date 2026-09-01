using Domain.Entities.Skills;
using Microsoft.EntityFrameworkCore;
using Persistence.Contracts;
using Persistence.Contracts.Repositories;

namespace Persistence.Repositories;

internal sealed class SkillRepository(IDbContext context) : ISkillRepository
{
    public Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Skills.FirstOrDefaultAsync(skill => skill.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Skill>> GetAvailableAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.Skills
            .Where(skill => skill.UserId == userId || skill.UserId == null)
            .OrderBy(skill => skill.Name)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Skill>> GetAvailableByIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await context.Skills
            .Where(skill => ids.Contains(skill.Id))
            .Where(skill => skill.UserId == userId || skill.UserId == null)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Skill?> FindByNameAsync(Guid userId, string name, CancellationToken cancellationToken = default) =>
        context.Skills.FirstOrDefaultAsync(
            skill => skill.UserId == userId && skill.Name == name,
            cancellationToken);

    /// <summary>
    /// Идём от агентов, а не от join-таблицы: отдельной сущности под связь нет, и в LINQ
    /// она недоступна. Запрос всё равно превращается в join по agent_skills.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>> GetAgentIdsAsync(
        Guid skillId,
        CancellationToken cancellationToken = default) =>
        await context.Agents
            .Where(agent => agent.Skills.Any(skill => skill.Id == skillId))
            .Select(agent => agent.Id)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(Skill skill, CancellationToken cancellationToken = default) =>
        await context.Skills.AddAsync(skill, cancellationToken);

    public void Remove(Skill skill) => context.Skills.Remove(skill);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
