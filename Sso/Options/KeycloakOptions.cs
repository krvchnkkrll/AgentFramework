namespace Sso.Options;

public sealed class KeycloakOptions
{
    public required string Authority { get; init; }
    public required string Audience { get; init; }
}