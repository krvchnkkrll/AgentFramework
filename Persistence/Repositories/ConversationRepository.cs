using Domain.Entities.Conversations;
using Microsoft.EntityFrameworkCore;
using Persistence.Contracts;
using Persistence.Contracts.Repositories;

namespace Persistence.Repositories;

internal sealed class ConversationRepository(IDbContext context) : IConversationRepository
{
    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Conversations
            .Include(conversation => conversation.Messages)
            .FirstOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Conversation>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.Conversations
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.IsPinned)
            .ThenByDescending(conversation => conversation.UpdatedAt)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) =>
        await context.Conversations.AddAsync(conversation, cancellationToken);

    public void Remove(Conversation conversation) => context.Conversations.Remove(conversation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
