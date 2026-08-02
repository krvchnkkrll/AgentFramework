using Microsoft.EntityFrameworkCore;
using Persistence.Contracts;

namespace Persistence;

internal sealed class DbContextFactory(IDbContextFactory<DbContext> factory) : IDbContextFactory
{
    public async Task<IDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await factory.CreateDbContextAsync(cancellationToken);
    }
}