using System.Security.Claims;
using Domain.Common;
using Microsoft.AspNetCore.Http;
using Persistence.Contracts.Services;

namespace Sso.Services;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Result<Guid> GetUserId()
    {
        var principalResult = GetPrincipal();
        if (principalResult.IsFailure)
            return Result.Failure<Guid>(principalResult.Error);

        var sub = principalResult.Value.FindFirstValue("sub");

        return string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var id)
            ? Result.Failure<Guid>(Error.Forbidden("Principal.InvalidSub", "Token does not contain a valid 'sub' claim."))
            : id;
    }

    public Result<string> GetEmail()
    {
        var principalResult = GetPrincipal();
        if (principalResult.IsFailure)
            return Result.Failure<string>(principalResult.Error);

        var email = principalResult.Value.FindFirstValue("email");

        return string.IsNullOrEmpty(email)
            ? Result.Failure<string>(Error.Forbidden("Principal.MissingEmail", "Token does not contain an 'email' claim."))
            : email;
    }

    public Result<string> GetUsername()
    {
        var principalResult = GetPrincipal();
        if (principalResult.IsFailure)
            return Result.Failure<string>(principalResult.Error);

        var username = principalResult.Value.FindFirstValue("preferred_username");

        return string.IsNullOrEmpty(username)
            ? Result.Failure<string>(Error.Forbidden("Principal.MissingUsername", "Token does not contain a 'preferred_username' claim."))
            : username;
    }

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var principalResult = GetPrincipal();
            return principalResult.IsFailure
                ? Array.Empty<string>()
                : principalResult.Value.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        }
    }

    private Result<ClaimsPrincipal> GetPrincipal() =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal
            ? principal
            : Result.Failure<ClaimsPrincipal>(Error.Forbidden("Principal.NotAuthenticated", "Current request is not authenticated."));
}
