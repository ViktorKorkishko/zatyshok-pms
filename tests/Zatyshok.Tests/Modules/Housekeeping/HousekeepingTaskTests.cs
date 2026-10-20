using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Housekeeping;

namespace Zatyshok.Tests.Modules.Housekeeping;

/// <summary>Cleaning task transitions of table 12.27 and the creation rules.</summary>
public class HousekeepingTaskTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid MaidId = Guid.NewGuid();
    private static readonly Guid OtherMaidId = Guid.NewGuid();
    private static readonly Guid HeadHousekeeperId = Guid.NewGuid();

    private static readonly Dictionary<string, Action<HousekeepingTask>> Actions = new()
    {
        ["Assign"] = t => t.Assign(OtherMaidId, Now),
        ["Start"] = t => t.Start(Now),
        ["Complete"] = t => t.Complete(Now),
        ["Cancel"] = t => t.Cancel(Now),
    };

    private static readonly Dictionary<(HousekeepingTaskStatus From, string Action), HousekeepingTaskStatus> Allowed = new()
    {
        [(HousekeepingTaskStatus.New, "Assign")] = HousekeepingTaskStatus.Assigned,
        [(HousekeepingTaskStatus.Assigned, "Assign")] = HousekeepingTaskStatus.Assigned,     // reassignment, UC-15 A1
        [(HousekeepingTaskStatus.InProgress, "Assign")] = HousekeepingTaskStatus.InProgress, // reassignment, UC-15 A1
        [(HousekeepingTaskStatus.Assigned, "Start")] = HousekeepingTaskStatus.InProgress,
        [(HousekeepingTaskStatus.InProgress, "Complete")] = HousekeepingTaskStatus.Done,
        [(HousekeepingTaskStatus.New, "Cancel")] = HousekeepingTaskStatus.Cancelled,
        [(HousekeepingTaskStatus.Assigned, "Cancel")] = HousekeepingTaskStatus.Cancelled,
        [(HousekeepingTaskStatus.InProgress, "Cancel")] = HousekeepingTaskStatus.Cancelled,
    };

    public static TheoryData<HousekeepingTaskStatus, string> AllPairs()
    {
        var data = new TheoryData<HousekeepingTaskStatus, string>();
        foreach (var status in Enum.GetValues<HousekeepingTaskStatus>())
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
    public void ActionIsAllowedOnlyFromStatesOfTable12_27(HousekeepingTaskStatus from, string action)
    {
        var task = TaskIn(from);

        if (Allowed.TryGetValue((from, action), out var expected))
        {
            Actions[action](task);
            Assert.Equal(expected, task.Status);
        }
        else
        {
            Assert.Throws<DomainException>(() => Actions[action](task));
            Assert.Equal(from, task.Status);
        }
    }

    [Fact]
    public void AssignedTaskCannotBeDoneWithoutStarting()
    {
        var task = TaskIn(HousekeepingTaskStatus.Assigned);

        Assert.Throws<DomainException>(() => task.Complete(Now));
    }

    [Fact]
    public void LifeCycleSetsAssigneeAndTimestamps()
    {
        var task = new HousekeepingTask(RoomId, HousekeepingTaskType.AfterDeparture, Now, bookingId: BookingId);

        task.Assign(MaidId, Now.AddMinutes(5));
        task.Start(Now.AddMinutes(30));
        task.Assign(OtherMaidId, Now.AddMinutes(40));
        task.Complete(Now.AddMinutes(60));

        Assert.Equal(HousekeepingTaskStatus.Done, task.Status);
        Assert.Equal(OtherMaidId, task.AssignedToUserId);
        Assert.Equal(Now.AddMinutes(40), task.AssignedAtUtc);
        Assert.Equal(Now.AddMinutes(30), task.StartedAtUtc);
        Assert.Equal(Now.AddMinutes(60), task.CompletedAtUtc);
        Assert.Null(task.CancelledAtUtc);
    }

    [Fact]
    public void CancelSetsTime()
    {
        var task = TaskIn(HousekeepingTaskStatus.InProgress);

        task.Cancel(Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), task.CancelledAtUtc);
    }

    [Fact]
    public void NewTaskHasDefaults()
    {
        var task = new HousekeepingTask(RoomId, HousekeepingTaskType.Routine, Now, createdByUserId: HeadHousekeeperId);

        Assert.Equal(HousekeepingTaskStatus.New, task.Status);
        Assert.Equal(HousekeepingTask.NormalPriority, task.Priority);
        Assert.Null(task.AssignedToUserId);
        Assert.Equal(Now, task.CreatedAtUtc);
    }

    [Fact]
    public void AfterDepartureTaskNeedsBookingAndIsCreatedBySystem()
    {
        Assert.Throws<DomainException>(() => new HousekeepingTask(RoomId, HousekeepingTaskType.AfterDeparture, Now));
        Assert.Throws<DomainException>(() => new HousekeepingTask(
            RoomId, HousekeepingTaskType.AfterDeparture, Now, bookingId: BookingId, createdByUserId: HeadHousekeeperId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void PriorityIsFrom1To3(short priority)
    {
        Assert.Throws<DomainException>(() => new HousekeepingTask(RoomId, HousekeepingTaskType.Deep, Now, priority: priority));
    }

    private static HousekeepingTask TaskIn(HousekeepingTaskStatus status)
    {
        var task = new HousekeepingTask(RoomId, HousekeepingTaskType.AfterDeparture, Now, bookingId: BookingId);
        switch (status)
        {
            case HousekeepingTaskStatus.New:
                break;
            case HousekeepingTaskStatus.Assigned:
                task.Assign(MaidId, Now);
                break;
            case HousekeepingTaskStatus.InProgress:
                task.Assign(MaidId, Now);
                task.Start(Now);
                break;
            case HousekeepingTaskStatus.Done:
                task.Assign(MaidId, Now);
                task.Start(Now);
                task.Complete(Now);
                break;
            case HousekeepingTaskStatus.Cancelled:
                task.Cancel(Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return task;
    }
}
