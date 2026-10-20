using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Identity;

namespace Zatyshok.Tests.Modules.Identity;

/// <summary>Account states of table 12.28 and the temporary lockout (FR-01, UC-02 A1).</summary>
public class UserTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RoleId = Guid.NewGuid();

    private static readonly Dictionary<string, Action<User>> Actions = new()
    {
        ["ConfirmEmail"] = u => u.ConfirmEmail(),
        ["Block"] = u => u.Block(),
        ["Unblock"] = u => u.Unblock(),
        ["RegisterSuccessfulLogin"] = u => u.RegisterSuccessfulLogin(Now),
        ["RegisterFailedLogin"] = u => u.RegisterFailedLogin(Now),
    };

    // PendingConfirmation → Active; Active ↔ Blocked; logins only for active accounts.
    private static readonly Dictionary<(UserStatus From, string Action), UserStatus> Allowed = new()
    {
        [(UserStatus.PendingConfirmation, "ConfirmEmail")] = UserStatus.Active,
        [(UserStatus.Active, "Block")] = UserStatus.Blocked,
        [(UserStatus.Blocked, "Unblock")] = UserStatus.Active,
        [(UserStatus.Active, "RegisterSuccessfulLogin")] = UserStatus.Active,
        [(UserStatus.Active, "RegisterFailedLogin")] = UserStatus.Active,
    };

    public static TheoryData<UserStatus, string> AllPairs()
    {
        var data = new TheoryData<UserStatus, string>();
        foreach (var status in Enum.GetValues<UserStatus>())
        {
            foreach (var action in Actions.Keys)
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void ActionIsAllowedOnlyFromStatesOfTable12_28(UserStatus from, string action)
    {
        var user = UserIn(from);

        if (Allowed.TryGetValue((from, action), out var expected))
        {
            Actions[action](user);
            Assert.Equal(expected, user.Status);
        }
        else
        {
            var error = Assert.Throws<InvalidTransitionException>(() => Actions[action](user));
            Assert.DoesNotContain(from.ToString(), error.Message);
            Assert.Equal(from, user.Status);
        }
    }

    [Fact]
    public void StaffAccountIsActiveWithTemporaryPassword()
    {
        var user = User.CreateStaff("  Olena.Receptionist@Zatyshok.UA ", "hash", "Олена Петренко", RoleId, Guid.NewGuid(), Now);

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.MustChangePassword);
        Assert.Equal("olena.receptionist@zatyshok.ua", user.Email);
        Assert.Equal(Now, user.CreatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StaffAccountRequiresFullName(string fullName)
    {
        Assert.Throws<DomainException>(() => User.CreateStaff("a@b.ua", "hash", fullName, RoleId, null, Now));
    }

    [Fact]
    public void GuestAccountWaitsForConfirmationAndHasNoFullName()
    {
        var user = User.RegisterGuest("Guest@Example.com", "hash", RoleId, Now);

        Assert.Equal(UserStatus.PendingConfirmation, user.Status);
        Assert.Null(user.FullName);
        Assert.False(user.MustChangePassword);
        Assert.Equal("guest@example.com", user.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmailIsRequired(string email)
    {
        Assert.Throws<DomainException>(() => User.RegisterGuest(email, "hash", RoleId, Now));
    }

    [Fact]
    public void FourFailedLoginsDoNotLockTheAccount()
    {
        var user = UserIn(UserStatus.Active);

        for (var i = 0; i < User.MaxFailedLoginAttempts - 1; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.Equal(4, user.FailedLoginCount);
        Assert.False(user.IsLockedOut(Now));
    }

    [Fact]
    public void FifthFailedLoginLocksFor15MinutesWithoutChangingTheState()
    {
        var user = UserIn(UserStatus.Active);

        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.Equal(Now.AddMinutes(15), user.LockoutEndUtc);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.IsLockedOut(Now.AddMinutes(14)));
        Assert.Throws<DomainException>(() => user.RegisterSuccessfulLogin(Now.AddMinutes(14)));
        Assert.Throws<DomainException>(() => user.RegisterFailedLogin(Now.AddMinutes(14)));
    }

    [Fact]
    public void SuccessfulLoginAfterLockoutClearsCounters()
    {
        var user = LockedOutUser();
        user.RegisterFailedLogin(Now.AddMinutes(15)); // lockout is over: counting starts again

        user.RegisterSuccessfulLogin(Now.AddMinutes(16));

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndUtc);
        Assert.Equal(Now.AddMinutes(16), user.LastLoginAtUtc);
    }

    [Fact]
    public void UnblockClearsLockout()
    {
        var user = LockedOutUser();
        user.Block();

        user.Unblock();

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.LockoutEndUtc);
        Assert.False(user.IsLockedOut(Now));
    }

    private static User LockedOutUser()
    {
        var user = UserIn(UserStatus.Active);
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        return user;
    }

    private static User UserIn(UserStatus status)
    {
        switch (status)
        {
            case UserStatus.PendingConfirmation:
                return User.RegisterGuest("guest@example.com", "hash", RoleId, Now);
            case UserStatus.Active:
                return User.CreateStaff("staff@zatyshok.ua", "hash", "Співробітник", RoleId, null, Now);
            case UserStatus.Blocked:
                var user = User.CreateStaff("staff@zatyshok.ua", "hash", "Співробітник", RoleId, null, Now);
                user.Block();
                return user;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }
}
