using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Identity;

/// <summary>
/// User account of a guest or a staff member (table 12.3). Accounts are created by
/// <see cref="CreateStaff"/> or <see cref="RegisterGuest"/>; the state and the login counters
/// change only through the methods below (table 12.28). Password hashing comes in T4.
/// </summary>
public class User
{
    /// <summary>Failed logins in a row that lock the account (FR-01, UC-02 A1).</summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>Length of the temporary lockout (UC-02 A1).</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // For EF Core and the factory methods.
    private User()
    {
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Login (FR-01), always lower case so that uniqueness ignores case (CK_Users_EmailLowerCase).</summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; set; } = null!;

    /// <summary>Full name of a staff member; null for guests, whose name is kept in the guest profile (3NF, 12.1.2).</summary>
    public string? FullName { get; set; }

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    /// <summary>Visibility scope: a hotel, or null for the whole network (FR-02). No navigation: Hotel belongs to the Rooms module.</summary>
    public Guid? HotelId { get; set; }

    public UserStatus Status { get; private set; } = UserStatus.PendingConfirmation;

    public int FailedLoginCount { get; private set; }

    public DateTime? LockoutEndUtc { get; private set; }

    /// <summary>The temporary password must be changed at the first login (UC-10).</summary>
    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Concurrency token mapped to the PostgreSQL system column xmin (12.3).</summary>
    public uint Version { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();

    /// <summary>Staff account created by the administrator: active at once, with a temporary password (UC-10, table 12.28).</summary>
    public static User CreateStaff(string email, string passwordHash, string fullName, Guid roleId, Guid? hotelId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("ПІБ співробітника обов'язкове.");
        }

        return new User
        {
            Email = NormalizeEmail(email),
            PasswordHash = RequirePasswordHash(passwordHash),
            FullName = fullName.Trim(),
            RoleId = roleId,
            HotelId = hotelId,
            Status = UserStatus.Active,
            MustChangePassword = true,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>Guest registered on the portal: waits for the email confirmation (UC-01).</summary>
    public static User RegisterGuest(string email, string passwordHash, Guid roleId, DateTime nowUtc)
        => new()
        {
            Email = NormalizeEmail(email),
            PasswordHash = RequirePasswordHash(passwordHash),
            RoleId = roleId,
            Status = UserStatus.PendingConfirmation,
            CreatedAtUtc = nowUtc,
        };

    /// <summary>Emails are stored and looked up in lower case (FR-01, UC-01 A1).</summary>
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Електронна пошта обов'язкова.");
        }

        return email.Trim().ToLowerInvariant();
    }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc > nowUtc;

    /// <summary>PendingConfirmation → Active: the guest followed the link from the email (UC-01).</summary>
    public void ConfirmEmail()
    {
        EnsureStatus("підтвердити пошту", UserStatus.PendingConfirmation);
        Status = UserStatus.Active;
    }

    /// <summary>
    /// Wrong password for an active account. The fifth failure in a row locks the account for
    /// 15 minutes and resets the counter; the state stays Active (table 12.28).
    /// </summary>
    public void RegisterFailedLogin(DateTime nowUtc)
    {
        EnsureCanSignIn("зареєструвати невдалий вхід", nowUtc);

        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLoginAttempts)
        {
            LockoutEndUtc = nowUtc + LockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        EnsureCanSignIn("увійти", nowUtc);

        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = nowUtc;
    }

    /// <summary>Active → Blocked by the administrator (FR-03). Revoking refresh tokens is done by the T4 service.</summary>
    public void Block()
    {
        EnsureStatus("заблокувати", UserStatus.Active);
        Status = UserStatus.Blocked;
    }

    /// <summary>Blocked → Active by the administrator (FR-03); a pending lockout is cleared too.</summary>
    public void Unblock()
    {
        EnsureStatus("розблокувати", UserStatus.Blocked);
        Status = UserStatus.Active;
        FailedLoginCount = 0;
        LockoutEndUtc = null;
    }

    private static string RequirePasswordHash(string passwordHash)
        => string.IsNullOrWhiteSpace(passwordHash)
            ? throw new DomainException("Хеш пароля обов'язковий.")
            : passwordHash;

    private void EnsureCanSignIn(string action, DateTime nowUtc)
    {
        EnsureStatus(action, UserStatus.Active);
        if (IsLockedOut(nowUtc))
        {
            throw new DomainException($"Обліковий запис тимчасово заблоковано до {LockoutEndUtc:yyyy-MM-dd HH:mm} UTC.");
        }
    }

    private void EnsureStatus(string action, UserStatus allowed)
    {
        if (Status != allowed)
        {
            throw DomainException.InvalidTransition("Обліковий запис", Status, action);
        }
    }
}
