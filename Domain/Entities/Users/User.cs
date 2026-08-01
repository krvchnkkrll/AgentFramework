using System.Net.Mail;
using Domain.Common;
using Domain.Entities.Users.Parameters;

namespace Domain.Entities.Users;

public sealed class User : Entity
{
    public string Email { get; private set; }
    public string Username { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? LastSeenAt { get; private set; }

    private User()
    {
        Email = null!;
        Username = null!;
    }

    private User(Guid id, string email, string username, DateTimeOffset createdAt) : base(id)
    {
        Email = email;
        Username = username;
        CreatedAt = createdAt;
    }
    
    public static User Create(CreateUserParameter parameter)
    {
        return new User(parameter.Id, NormalizeEmail(parameter.Email), NormalizeUsername(parameter.Username), DateTimeOffset.UtcNow);
    }

    public void SyncProfile(SyncProfileParameter parameter)
    {
        Email = NormalizeEmail(parameter.Email);
        Username = NormalizeUsername(parameter.Username);
    }

    public void RecordSignIn() => LastSeenAt = DateTimeOffset.UtcNow;

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var trimmed = email.Trim();

        try
        {
            if (new MailAddress(trimmed).Address != trimmed)
                throw new ArgumentException($"'{email}' is not a valid email address.", nameof(email));
        }
        catch (FormatException)
        {
            throw new ArgumentException($"'{email}' is not a valid email address.", nameof(email));
        }

        return trimmed;
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        var trimmed = username.Trim();

        return trimmed;
    }
}
