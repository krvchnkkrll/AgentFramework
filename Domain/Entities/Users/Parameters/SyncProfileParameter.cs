namespace Domain.Entities.Users.Parameters;

public readonly struct SyncProfileParameter
{
    public required string Email { get; init; }
    public required string Username { get; init; }
}