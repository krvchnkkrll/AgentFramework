using Domain.Entities.Agents;
using Domain.Entities.Conversations;
using Domain.Entities.Messages;
using Domain.Entities.Skills;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Contracts;

public interface IDbContext
{
    DbSet<User> Users { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<Agent> Agents { get; }
    DbSet<Skill> Skills { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
