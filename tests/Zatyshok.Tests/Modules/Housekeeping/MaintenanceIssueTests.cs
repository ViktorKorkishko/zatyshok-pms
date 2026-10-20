using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Housekeeping;

namespace Zatyshok.Tests.Modules.Housekeeping;

/// <summary>A fault is open until resolved once (table 12.18).</summary>
public class MaintenanceIssueTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid MaidId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();

    [Fact]
    public void NewIssueIsOpen()
    {
        var issue = new MaintenanceIssue(RoomId, "  Тече кран у ванній ", MaidId, Now);

        Assert.True(issue.IsOpen);
        Assert.Equal("Тече кран у ванній", issue.Description);
        Assert.Equal(Now, issue.ReportedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void DescriptionIsRequired(string description)
    {
        Assert.Throws<DomainException>(() => new MaintenanceIssue(RoomId, description, MaidId, Now));
    }

    [Fact]
    public void ResolveClosesTheIssueOnce()
    {
        var issue = new MaintenanceIssue(RoomId, "Не працює кондиціонер", MaidId, Now);

        issue.Resolve(ManagerId, "Замінено компресор", Now.AddDays(1));

        Assert.False(issue.IsOpen);
        Assert.Equal(ManagerId, issue.ResolvedByUserId);
        Assert.Equal(Now.AddDays(1), issue.ResolvedAtUtc);
        Assert.Throws<DomainException>(() => issue.Resolve(ManagerId, null, Now.AddDays(2)));
    }

    [Fact]
    public void ResolutionCannotPrecedeReport()
    {
        var issue = new MaintenanceIssue(RoomId, "Не працює кондиціонер", MaidId, Now);

        Assert.Throws<DomainException>(() => issue.Resolve(ManagerId, null, Now.AddMinutes(-1)));
        Assert.True(issue.IsOpen);
    }
}
