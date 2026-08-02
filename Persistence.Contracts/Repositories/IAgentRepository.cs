using Domain.Entities.Agents;

namespace Persistence.Contracts.Repositories;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Agent>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Agent agent, CancellationToken cancellationToken = default);

    void Remove(Agent agent);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
