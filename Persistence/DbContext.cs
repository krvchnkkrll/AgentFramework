using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Persistence.Contracts;

namespace Persistence;

public sealed class DbContext(DbContextOptions<DbContext> options) : Microsoft.EntityFrameworkCore.DbContext(options), IDbContext
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
