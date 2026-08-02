namespace Persistence.Contracts;

public interface IDbContextFactory
{
    Task<IDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}