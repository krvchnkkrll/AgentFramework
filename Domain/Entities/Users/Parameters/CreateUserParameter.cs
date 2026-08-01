namespace Domain.Entities.Users.Parameters;

public readonly struct CreateUserParameter
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Username { get; init; }
}