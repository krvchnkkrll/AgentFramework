using Domain.Entities.Agents;
using Microsoft.EntityFrameworkCore;
using Persistence.Contracts;
using Persistence.Contracts.Repositories;

namespace Persistence.Repositories;

internal sealed class AgentRepository(IDbContext context) : IAgentRepository
{
    /// <summary>
    /// Скиллы подтягиваем всегда: без них агента не отдать ни в конструктор, ни ассистенту,
    /// а отдельного сценария «агент без скиллов» у репозитория нет.
    /// </summary>
    public Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Agents
            .Include(agent => agent.Skills)
            .FirstOrDefaultAsync(agent => agent.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Agent>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.Agents
            .Include(agent => agent.Skills)
            .Where(agent => agent.UserId == userId)
            .OrderBy(agent => agent.Name)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(Agent agent, CancellationToken cancellationToken = default) =>
        await context.Agents.AddAsync(agent, cancellationToken);

    public void Remove(Agent agent) => context.Agents.Remove(agent);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
