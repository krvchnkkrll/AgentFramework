using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Sso.Options;

namespace Sso;

internal sealed class KeycloakRoleClaimsTransformation(IOptions<KeycloakOptions> keycloakOptions) : IClaimsTransformation
{
    private readonly string _audience = keycloakOptions.Value.Audience;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);
        
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role))
            return Task.FromResult(principal);

        foreach (var role in ExtractRealmRoles(principal))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        foreach (var role in ExtractClientRoles(principal))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        return Task.FromResult(principal);
    }

    private static IEnumerable<string> ExtractRealmRoles(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirstValue("realm_access");
        if (string.IsNullOrEmpty(realmAccess))
            yield break;

        using var document = JsonDocument.Parse(realmAccess);
        if (!document.RootElement.TryGetProperty("roles", out var roles))
            yield break;

        foreach (var role in roles.EnumerateArray())
        {
            var value = role.GetString();
            if (!string.IsNullOrEmpty(value))
                yield return value;
        }
    }

    private IEnumerable<string> ExtractClientRoles(ClaimsPrincipal principal)
    {
        if (string.IsNullOrEmpty(_audience))
            yield break;

        var resourceAccess = principal.FindFirstValue("resource_access");
        if (string.IsNullOrEmpty(resourceAccess))
            yield break;

        using var document = JsonDocument.Parse(resourceAccess);
        if (!document.RootElement.TryGetProperty(_audience, out var client) ||
            !client.TryGetProperty("roles", out var roles))
            yield break;

        foreach (var role in roles.EnumerateArray())
        {
            var value = role.GetString();
            if (!string.IsNullOrEmpty(value))
                yield return value;
        }
    }
}
