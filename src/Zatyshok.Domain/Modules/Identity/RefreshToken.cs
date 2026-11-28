namespace Zatyshok.Domain.Modules.Identity;

/// <summary>
/// Refresh token of a JWT session (table 12.4). Only the hash is stored. Tokens of one
/// session replace each other on rotation; revoking them ends all sessions (UC-03).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Chain of tokens that replace each other.</summary>
    public Guid SessionId { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Sliding expiry: 30 minutes of inactivity for staff (NFR-07).</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Absolute end of the session: 12 hours for staff, 30 days for guests (NFR-07).</summary>
    public DateTime SessionExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Token issued instead of this one on rotation; no foreign key.</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
