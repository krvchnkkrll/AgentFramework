using Domain.Entities.Conversations;

namespace Persistence.Contracts.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Conversation>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    void Remove(Conversation conversation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
