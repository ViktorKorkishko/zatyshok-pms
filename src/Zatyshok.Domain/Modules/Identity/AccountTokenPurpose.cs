namespace Zatyshok.Domain.Modules.Identity;

/// <summary>Purpose of an <see cref="AccountToken"/> (table 12.24).</summary>
public enum AccountTokenPurpose
{
    EmailConfirmation,
    PasswordReset,
}
