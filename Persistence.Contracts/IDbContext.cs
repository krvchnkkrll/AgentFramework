using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Contracts;

public interface IDbContext
{
    DbSet<User> Users { get; }
}