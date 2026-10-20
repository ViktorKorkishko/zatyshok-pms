using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Modules.Rooms;

/// <summary>Room state transitions of table 12.26: every (state, action) pair is either allowed or rejected with <see cref="InvalidTransitionException"/>.</summary>
public class RoomTransitionTests
{
    private static readonly Dictionary<string, Action<Room>> Actions = new()
    {
        ["Reserve"] = r => r.Reserve(),
        ["Release"] = r => r.Release(),
        ["Occupy"] = r => r.Occupy(),
        ["MarkDirty"] = r => r.MarkDirty(),
        ["MarkClean"] = r => r.MarkClean(),
        ["MarkOutOfOrder"] = r => r.MarkOutOfOrder(),
        ["ReturnToService"] = r => r.ReturnToService(),
    };

    // Allowed transitions of table 12.26; every other pair must throw.
    private static readonly Dictionary<(RoomStatus From, string Action), RoomStatus> Allowed = new()
    {
        [(RoomStatus.Free, "Reserve")] = RoomStatus.Reserved,
        [(RoomStatus.Reserved, "Release")] = RoomStatus.Free,
        [(RoomStatus.Free, "Occupy")] = RoomStatus.Occupied,
        [(RoomStatus.Reserved, "Occupy")] = RoomStatus.Occupied,
        [(RoomStatus.Occupied, "MarkDirty")] = RoomStatus.Dirty,
        [(RoomStatus.Dirty, "MarkClean")] = RoomStatus.Free,
        [(RoomStatus.Free, "MarkOutOfOrder")] = RoomStatus.OutOfOrder,
        [(RoomStatus.Dirty, "MarkOutOfOrder")] = RoomStatus.OutOfOrder,
        [(RoomStatus.OutOfOrder, "ReturnToService")] = RoomStatus.Dirty,
    };

    public static TheoryData<RoomStatus, string> AllPairs()
    {
        var data = new TheoryData<RoomStatus, string>();
        foreach (var status in Enum.GetValues<RoomStatus>())
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
    public void ActionIsAllowedOnlyFromStatesOfTable12_26(RoomStatus from, string action)
    {
        var room = RoomIn(from);

        if (Allowed.TryGetValue((from, action), out var expected))
        {
            Actions[action](room);
            Assert.Equal(expected, room.Status);
        }
        else
        {
            Assert.Throws<InvalidTransitionException>(() => Actions[action](room));
            Assert.Equal(from, room.Status);
        }
    }

    [Theory]
    [InlineData(RoomStatus.Free)]
    [InlineData(RoomStatus.Reserved)]
    [InlineData(RoomStatus.Dirty)]
    [InlineData(RoomStatus.OutOfOrder)]
    public void DeactivateIsIndependentOfStateExceptOccupied(RoomStatus status)
    {
        var room = RoomIn(status);

        room.Deactivate();

        Assert.False(room.IsActive);
        Assert.Equal(status, room.Status);
    }

    [Fact]
    public void OccupiedRoomCannotBeDeactivated()
    {
        var room = RoomIn(RoomStatus.Occupied);

        Assert.Throws<DomainException>(room.Deactivate);
        Assert.True(room.IsActive);
    }

    [Fact]
    public void DeactivatedRoomCannotBeReservedOrOccupiedUntilActivated()
    {
        var room = NewRoom();
        room.Deactivate();

        Assert.Throws<DomainException>(room.Reserve);
        Assert.Throws<DomainException>(room.Occupy);

        room.Activate();
        room.Occupy();
        Assert.Equal(RoomStatus.Occupied, room.Status);
    }

    [Fact]
    public void NewRoomIsFreeAndActive()
    {
        var room = NewRoom();

        Assert.Equal(RoomStatus.Free, room.Status);
        Assert.True(room.IsActive);
    }

    private static Room NewRoom() => new() { Number = "101", Floor = 1, Capacity = 2, RoomCategoryId = Guid.NewGuid() };

    // Brings a new room to the given state through allowed transitions only.
    internal static Room RoomIn(RoomStatus status)
    {
        var room = NewRoom();
        switch (status)
        {
            case RoomStatus.Free:
                break;
            case RoomStatus.Reserved:
                room.Reserve();
                break;
            case RoomStatus.Occupied:
                room.Occupy();
                break;
            case RoomStatus.Dirty:
                room.Occupy();
                room.MarkDirty();
                break;
            case RoomStatus.OutOfOrder:
                room.MarkOutOfOrder();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return room;
    }
}
