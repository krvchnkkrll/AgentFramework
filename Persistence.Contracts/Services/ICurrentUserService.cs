using Domain.Common;

namespace Persistence.Contracts.Services;

public interface ICurrentUserService
{
    Result<Guid> GetUserId();

    Result<string> GetEmail();

    Result<string> GetUsername();

    IReadOnlyCollection<string> Roles { get; }
}
