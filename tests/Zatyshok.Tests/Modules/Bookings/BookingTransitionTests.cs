using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Bookings;
using static Zatyshok.Tests.Modules.Bookings.BookingTestData;

namespace Zatyshok.Tests.Modules.Bookings;

/// <summary>
/// Table 12.25 as a matrix: every (state, method) pair is either allowed or rejected with
/// <see cref="InvalidTransitionException"/>, and a rejected call changes neither the state nor the
/// history. The exact exception type matters: a method may also throw a plain
/// <see cref="DomainException"/> for another rule (for example, "the room is already assigned"),
/// which would hide a missing state check.
/// </summary>
public class BookingTransitionTests
{
    // Methods that change the state. Their non-state conditions (dates, room category) are met,
    // so the outcome depends only on the current state.
    private static readonly Dictionary<string, Action<Booking>> TransitionActions = new()
    {
        ["Confirm"] = b => b.Confirm(OperatorId, Now),
        ["Cancel"] = b => b.Cancel("Причина", byGuest: false, OperatorId, Now),
        ["MarkNoShow"] = b => b.MarkNoShow(CheckIn.AddDays(1), Now),
        ["CheckIn"] = b => b.CheckIn(b.RoomId ?? RoomId, StandardId, CheckIn, OperatorId, Now),
        ["CheckOut"] = b => b.CheckOut(OperatorId, Now),
    };

    private static readonly Dictionary<(BookingStatus From, string Action), BookingStatus> AllowedTransitions = new()
    {
        [(BookingStatus.New, "Confirm")] = BookingStatus.Confirmed,
        [(BookingStatus.New, "Cancel")] = BookingStatus.Cancelled,
        [(BookingStatus.Confirmed, "Cancel")] = BookingStatus.Cancelled,
        [(BookingStatus.Confirmed, "CheckIn")] = BookingStatus.CheckedIn,
        [(BookingStatus.Confirmed, "MarkNoShow")] = BookingStatus.NoShow,
        [(BookingStatus.CheckedIn, "CheckOut")] = BookingStatus.Completed,
    };

    // Changes without a state transition (table 12.25, "Зміни без переходу стану"):
    // an optional arrangement and the call itself.
    private static readonly Dictionary<string, (Action<Booking>? Arrange, Action<Booking> Act)> ChangeActions = new()
    {
        ["AssignRoom"] = (null, b => b.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now)),
        ["UnassignRoom"] = (
            b =>
            {
                if (b.Status == BookingStatus.Confirmed)
                {
                    b.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);
                }
            },
            b => b.UnassignRoom(OperatorId, Now)),
        // Only the check-out date changes, so a room on the booking (CheckedIn, Completed) does not
        // trigger the "remove the pre-assignment first" rule instead of the state check.
        ["ChangeDates"] = (null, b => b.ChangeDates(
            CheckIn, CheckOut.AddDays(1), Nights(CheckIn, CheckOut.AddDays(1)), Deadline, OperatorId, Now)),
        ["ChangeCategory"] = (null, b => b.ChangeCategory(SuiteId, Nights(CheckIn, CheckOut, SuitePrice), OperatorId, Now)),
        ["ChangeGuestsCount"] = (null, b => b.ChangeGuestsCount(3, OperatorId, Now)),
        ["ChangeWishes"] = (null, b => b.ChangeWishes("Високий поверх", OperatorId, Now)),
        ["UpgradeCategory"] = (null, b => b.UpgradeCategory(SuiteId, CheckIn, "Standard зайнято", OperatorId, Now)),
        ["Extend"] = (null, b => b.Extend(CheckOut.AddDays(1), Nights(CheckOut, CheckOut.AddDays(1)), OperatorId, Now)),
        ["ShortenStay"] = (null, b => b.ShortenStay(CheckIn.AddDays(1), CheckIn, OperatorId, Now)),
        ["MoveToRoom"] = (null, b => b.MoveToRoom(OtherRoomId, StandardId, "Не працює кондиціонер", OperatorId, Now)),
        ["RecordPayment"] = (null, b => b.RecordPayment(1000m, "Передоплата карткою", OperatorId, Now)),
    };

    private static readonly Dictionary<string, BookingStatus[]> AllowedChanges = new()
    {
        ["AssignRoom"] = [BookingStatus.Confirmed],
        ["UnassignRoom"] = [BookingStatus.Confirmed],
        ["ChangeDates"] = [BookingStatus.New, BookingStatus.Confirmed],
        ["ChangeCategory"] = [BookingStatus.New, BookingStatus.Confirmed],
        ["ChangeGuestsCount"] = [BookingStatus.New, BookingStatus.Confirmed],
        ["ChangeWishes"] = [BookingStatus.New, BookingStatus.Confirmed],
        ["UpgradeCategory"] = [BookingStatus.Confirmed],
        ["Extend"] = [BookingStatus.CheckedIn],
        ["ShortenStay"] = [BookingStatus.CheckedIn],
        ["MoveToRoom"] = [BookingStatus.CheckedIn],
        ["RecordPayment"] = Enum.GetValues<BookingStatus>(),
    };

    public static TheoryData<BookingStatus, string> AllTransitionPairs() => Pairs(TransitionActions.Keys);

    public static TheoryData<BookingStatus, string> AllChangePairs() => Pairs(ChangeActions.Keys);

    [Theory]
    [MemberData(nameof(AllTransitionPairs))]
    public void TransitionIsAllowedOnlyFromStatesOfTable12_25(BookingStatus from, string action)
    {
        var booking = InStatus(from);
        var historyCount = booking.History.Count;

        if (AllowedTransitions.TryGetValue((from, action), out var expected))
        {
            TransitionActions[action](booking);

            Assert.Equal(expected, booking.Status);
            var record = booking.History.Last();
            Assert.Equal(BookingHistoryEventType.StatusChanged, record.EventType);
            Assert.Equal("Status", record.FieldName);
            Assert.Equal(from.ToString(), record.OldValue);
            Assert.Equal(expected.ToString(), record.NewValue);
        }
        else
        {
            var error = Assert.Throws<InvalidTransitionException>(() => TransitionActions[action](booking));

            // The message names the state in Ukrainian, not by the enum name (NFR-13).
            Assert.DoesNotContain(from.ToString(), error.Message);
            Assert.Equal(from, booking.Status);
            Assert.Equal(historyCount, booking.History.Count);
        }
    }

    [Theory]
    [MemberData(nameof(AllChangePairs))]
    public void ChangeIsAllowedOnlyInStatesOfTable12_25(BookingStatus status, string action)
    {
        var booking = InStatus(status);
        var (arrange, act) = ChangeActions[action];
        arrange?.Invoke(booking);
        var historyCount = booking.History.Count;

        if (AllowedChanges[action].Contains(status))
        {
            act(booking);

            Assert.Equal(status, booking.Status);
            Assert.True(booking.History.Count > historyCount, "the change must be written to the history");
        }
        else
        {
            var error = Assert.Throws<InvalidTransitionException>(() => act(booking));

            Assert.DoesNotContain(status.ToString(), error.Message);
            Assert.Equal(status, booking.Status);
            Assert.Equal(historyCount, booking.History.Count);
        }
    }

    [Theory]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.NoShow)]
    public void FinalStatesAllowNoTransition(BookingStatus status)
    {
        foreach (var action in TransitionActions.Values)
        {
            var booking = InStatus(status);
            Assert.Throws<InvalidTransitionException>(() => action(booking));
        }
    }

    [Fact]
    public void ForbiddenTransitionMessageNamesTheStateInUkrainian()
    {
        var booking = InStatus(BookingStatus.Completed);

        var error = Assert.Throws<InvalidTransitionException>(() => booking.Cancel("Причина", byGuest: false, OperatorId, Now));

        Assert.Equal("Бронювання: дію «скасувати» заборонено в стані «завершене».", error.Message);
    }

    private static TheoryData<BookingStatus, string> Pairs(IEnumerable<string> actions)
    {
        var data = new TheoryData<BookingStatus, string>();
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            foreach (var action in actions)
            {
                data.Add(status, action);
            }
        }

        return data;
    }
}
