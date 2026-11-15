namespace Zatyshok.Domain.Modules.Identity;

/// <summary>One-time token from an email link: email confirmation or password reset (table 12.5).</summary>
public class AccountToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public AccountTokenPurpose Purpose { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>24 hours for email confirmation (UC-01), 1 hour for password reset (UC-03).</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>A used token is not accepted again (UC-03 A1).</summary>
    public DateTime? UsedAtUtc { get; set; }
}
