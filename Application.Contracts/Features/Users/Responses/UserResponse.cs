namespace Application.Contracts.Features.Users.Responses;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string Username,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt);
