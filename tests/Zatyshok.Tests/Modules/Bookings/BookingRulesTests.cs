using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Bookings;
using static Zatyshok.Tests.Modules.Bookings.BookingTestData;

namespace Zatyshok.Tests.Modules.Bookings;

/// <summary>Conditions of the transitions in table 12.25 besides the state, the night invariant and the history records.</summary>
public class BookingRulesTests
{
    [Fact]
    public void NewBookingHasNightsTotalAndCreatedRecord()
    {
        var booking = NewBooking();

        Assert.Equal(BookingStatus.New, booking.Status);
        Assert.Equal([CheckIn, CheckIn.AddDays(1)], booking.Nights.Select(n => n.Night));
        Assert.All(booking.Nights, n => Assert.Equal(booking.Id, n.BookingId));
        Assert.Equal(5000m, booking.TotalPrice);
        Assert.Null(booking.RoomId);
        Assert.Equal(CreatedAt, booking.CreatedAtUtc);

        var record = Assert.Single(booking.History);
        Assert.Equal(BookingHistoryEventType.Created, record.EventType);
        Assert.Equal(OperatorId, record.AuthorUserId);
        Assert.Equal(CreatedAt, record.OccurredAtUtc);
        Assert.Equal(booking.Id, record.BookingId);
    }

    [Fact]
    public void TotalPriceIsTheSumOfDifferentNightPrices()
    {
        var booking = new Booking(
            HotelId, StandardId, GuestId, null, CheckIn, CheckIn.AddDays(3), 1, BookingSource.Portal, null,
            [new(CheckIn, 1500m), new(CheckIn.AddDays(1), 2100m), new(CheckIn.AddDays(2), 2100m)],
            Deadline, null, CreatedAt);

        Assert.Equal(5700m, booking.TotalPrice);
    }

    public static TheoryData<string, List<NightPrice>> WrongNights() => new()
    {
        { "missing night", Nights(CheckIn, CheckIn.AddDays(1)) },
        { "extra night after check-out", Nights(CheckIn, CheckOut.AddDays(1)) },
        { "night before check-in", Nights(CheckIn.AddDays(-1), CheckOut) },
        { "duplicate night", [new(CheckIn, StandardPrice), new(CheckIn, StandardPrice), new(CheckIn.AddDays(1), StandardPrice)] },
        { "negative price", [new(CheckIn, StandardPrice), new(CheckIn.AddDays(1), -1m)] },
    };

    [Theory]
    [MemberData(nameof(WrongNights))]
    public void NightsMustBeExactlyTheDatesOfTheStay(string reason, List<NightPrice> nights)
    {
        var ex = Record.Exception(() => new Booking(
            HotelId, StandardId, GuestId, null, CheckIn, CheckOut, 2, BookingSource.Phone, null, nights, Deadline, OperatorId, CreatedAt));

        Assert.True(ex is DomainException, reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CheckOutMustBeAfterCheckIn(int nights)
    {
        Assert.Throws<DomainException>(() => new Booking(
            HotelId, StandardId, GuestId, null, CheckIn, CheckIn.AddDays(nights), 2, BookingSource.Phone, null, [], Deadline, OperatorId, CreatedAt));
    }

    [Fact]
    public void GuestsCountMustBePositive()
    {
        Assert.Throws<DomainException>(() => new Booking(
            HotelId, StandardId, GuestId, null, CheckIn, CheckOut, 0, BookingSource.Phone, null, Nights(CheckIn, CheckOut), Deadline, OperatorId, CreatedAt));

        var booking = NewBooking();
        Assert.Throws<DomainException>(() => booking.ChangeGuestsCount(0, OperatorId, Now));
    }

    [Theory]
    [InlineData(BookingStatus.New)]
    [InlineData(BookingStatus.Confirmed)]
    public void GuestCancelsBeforeDeadlineWithoutReason(BookingStatus status)
    {
        var booking = InStatus(status);

        booking.Cancel(null, byGuest: true, GuestId, Deadline.AddSeconds(-1));

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Null(booking.CancellationReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GuestCannotCancelAtOrAfterDeadline(int secondsAfterDeadline)
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.Cancel(null, byGuest: true, GuestId, Deadline.AddSeconds(secondsAfterDeadline)));
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void OperatorCancelsAfterDeadlineWithReason()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        booking.Cancel("  Гість захворів ", byGuest: false, OperatorId, Deadline.AddDays(1));

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal("Гість захворів", booking.CancellationReason);
        Assert.Equal("Гість захворів", booking.History.Last().Comment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OperatorMustGiveCancellationReason(string? reason)
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.Cancel(reason, byGuest: false, OperatorId, Now));
    }

    [Fact]
    public void CancelRemovesPreAssignedRoomAndRecordsIt()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);

        booking.Cancel("Гість передумав", byGuest: false, OperatorId, Now);

        Assert.Null(booking.RoomId);
        var roomRecord = booking.History.Single(h => h.FieldName == nameof(Booking.RoomId) && h.NewValue is null);
        Assert.Equal(RoomId.ToString(), roomRecord.OldValue);
    }

    [Fact]
    public void NoShowIsNotAllowedOnTheArrivalDay()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.MarkNoShow(CheckIn, Now));
    }

    [Fact]
    public void NoShowIsRecordedBySystemAndReleasesRoom()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);

        booking.MarkNoShow(CheckIn.AddDays(1), Now.AddDays(1));

        Assert.Equal(BookingStatus.NoShow, booking.Status);
        Assert.Null(booking.RoomId);
        Assert.Null(booking.History.Last().AuthorUserId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void RoomIsPreAssignedOnlyOnTheArrivalDay(int daysFromArrival)
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.AssignRoom(RoomId, StandardId, CheckIn.AddDays(daysFromArrival), OperatorId, Now));
    }

    [Fact]
    public void PreAssignedRoomMustBeOfTheBookedCategory()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.AssignRoom(RoomId, SuiteId, CheckIn, OperatorId, Now));
    }

    [Fact]
    public void SecondPreAssignmentNeedsUnassignFirst()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);

        Assert.Throws<DomainException>(() => booking.AssignRoom(OtherRoomId, StandardId, CheckIn, OperatorId, Now));

        booking.UnassignRoom(OperatorId, Now);
        booking.AssignRoom(OtherRoomId, StandardId, CheckIn, OperatorId, Now);
        Assert.Equal(OtherRoomId, booking.RoomId);
    }

    [Fact]
    public void UnassignWithoutRoomIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.UnassignRoom(OperatorId, Now));
    }

    [Fact]
    public void CheckInBeforeArrivalDayIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.CheckIn(RoomId, StandardId, CheckIn.AddDays(-1), OperatorId, Now));
    }

    [Fact]
    public void LateArrivalBeforeCheckOutDateIsCheckedIn()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        booking.CheckIn(RoomId, StandardId, CheckIn.AddDays(1), OperatorId, Now.AddDays(1));

        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
    }

    [Fact]
    public void CheckInAfterTheStayEndedIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.CheckIn(RoomId, StandardId, CheckOut, OperatorId, Now));
    }

    [Fact]
    public void CheckInRoomMustBeOfTheBookedCategory()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.CheckIn(RoomId, SuiteId, CheckIn, OperatorId, Now));
    }

    [Fact]
    public void CheckInToAnotherRoomThanPreAssignedIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);

        Assert.Throws<DomainException>(() => booking.CheckIn(OtherRoomId, StandardId, CheckIn, OperatorId, Now));
    }

    [Fact]
    public void CheckInSetsRoomAndTimeAndRecordsBoth()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        var historyCount = booking.History.Count;

        booking.CheckIn(RoomId, StandardId, CheckIn, OperatorId, Now);

        Assert.Equal(RoomId, booking.RoomId);
        Assert.Equal(Now, booking.CheckedInAtUtc);
        var records = booking.History.Skip(historyCount).ToList();
        Assert.Equal([BookingHistoryEventType.FieldChanged, BookingHistoryEventType.StatusChanged], records.Select(r => r.EventType));
        Assert.Equal(nameof(Booking.RoomId), records[0].FieldName);
    }

    [Fact]
    public void CheckInToPreAssignedRoomRecordsOnlyTheStatus()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);
        var historyCount = booking.History.Count;

        booking.CheckIn(RoomId, StandardId, CheckIn, OperatorId, Now);

        var record = Assert.Single(booking.History.Skip(historyCount));
        Assert.Equal(BookingHistoryEventType.StatusChanged, record.EventType);
    }

    [Fact]
    public void CheckOutKeepsRoomAndSetsTime()
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        booking.CheckOut(OperatorId, Now.AddDays(2));

        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.Equal(RoomId, booking.RoomId);
        Assert.Equal(Now.AddDays(2), booking.CheckedOutAtUtc);
        Assert.Equal(Now.AddDays(2), booking.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeDatesRecalculatesNightsTotalAndDeadlineAndRecordsEachField()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        var historyCount = booking.History.Count;
        var newDeadline = Deadline.AddDays(1);

        booking.ChangeDates(CheckIn.AddDays(1), CheckOut.AddDays(2), Nights(CheckIn.AddDays(1), CheckOut.AddDays(2), 2000m), newDeadline, OperatorId, Now);

        Assert.Equal(CheckIn.AddDays(1), booking.CheckInDate);
        Assert.Equal(CheckOut.AddDays(2), booking.CheckOutDate);
        Assert.Equal([CheckIn.AddDays(1), CheckIn.AddDays(2), CheckIn.AddDays(3)], booking.Nights.Select(n => n.Night));
        Assert.Equal(6000m, booking.TotalPrice);
        Assert.Equal(newDeadline, booking.FreeCancellationUntilUtc);

        // UC-17: "дата виїзду: 12.11 → 14.11" is stored as FieldChanged with ISO dates.
        var records = booking.History.Skip(historyCount).ToList();
        Assert.All(records, r => Assert.Equal(BookingHistoryEventType.FieldChanged, r.EventType));
        Assert.Equal(["CheckInDate", "CheckOutDate", "TotalPrice", "FreeCancellationUntilUtc"], records.Select(r => r.FieldName));
        Assert.Equal(("2026-11-12", "2026-11-14"), (records[1].OldValue, records[1].NewValue));
        Assert.Equal(("5000.00", "6000.00"), (records[2].OldValue, records[2].NewValue));

        // The free cancellation deadline (FR-15) as a UTC moment in ISO format.
        Assert.Equal(("2026-11-09T16:00:00Z", "2026-11-10T16:00:00Z"), (records[3].OldValue, records[3].NewValue));
    }

    [Fact]
    public void ChangeDatesKeepsRowsOfRemainingNights()
    {
        var booking = NewBooking();
        var secondNight = booking.Nights.Single(n => n.Night == CheckIn.AddDays(1));

        booking.ChangeDates(CheckIn.AddDays(1), CheckOut.AddDays(1), Nights(CheckIn.AddDays(1), CheckOut.AddDays(1), 2600m), Deadline, OperatorId, Now);

        // The same object is updated, so EF Core issues an UPDATE instead of DELETE + INSERT with the same key.
        Assert.Same(secondNight, booking.Nights.Single(n => n.Night == CheckIn.AddDays(1)));
        Assert.Equal(2600m, secondNight.Price);
    }

    [Fact]
    public void ChangeDatesWithSameDatesChangesNothing()
    {
        var booking = NewBooking();
        var historyCount = booking.History.Count;

        booking.ChangeDates(CheckIn, CheckOut, Nights(CheckIn, CheckOut), Deadline, OperatorId, Now);

        Assert.Equal(historyCount, booking.History.Count);
        Assert.Null(booking.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeDatesRejectsNightsThatDoNotMatch()
    {
        var booking = NewBooking();

        Assert.Throws<DomainException>(() => booking.ChangeDates(CheckIn, CheckOut.AddDays(1), Nights(CheckIn, CheckOut), Deadline, OperatorId, Now));
        Assert.Equal(CheckOut, booking.CheckOutDate);
    }

    [Fact]
    public void MovingCheckInWithPreAssignedRoomIsRejectedButCheckOutMayChange()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);

        Assert.Throws<DomainException>(() => booking.ChangeDates(
            CheckIn.AddDays(1), CheckOut.AddDays(1), Nights(CheckIn.AddDays(1), CheckOut.AddDays(1)), Deadline, OperatorId, Now));

        booking.ChangeDates(CheckIn, CheckOut.AddDays(1), Nights(CheckIn, CheckOut.AddDays(1)), Deadline, OperatorId, Now);
        Assert.Equal(CheckOut.AddDays(1), booking.CheckOutDate);
    }

    [Fact]
    public void ChangeCategoryRepricesNights()
    {
        var booking = NewBooking();

        booking.ChangeCategory(SuiteId, Nights(CheckIn, CheckOut, SuitePrice), OperatorId, Now);

        Assert.Equal(SuiteId, booking.RoomCategoryId);
        Assert.All(booking.Nights, n => Assert.Equal(SuitePrice, n.Price));
        Assert.Equal(12000m, booking.TotalPrice);
        Assert.Contains(booking.History, h => h.FieldName == nameof(Booking.RoomCategoryId));
    }

    [Fact]
    public void ChangeCategoryWithPreAssignedRoomIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);

        Assert.Throws<DomainException>(() => booking.ChangeCategory(SuiteId, Nights(CheckIn, CheckOut, SuitePrice), OperatorId, Now));
    }

    [Fact]
    public void UpgradeKeepsNightPricesAndRecordsTheDecision()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        booking.UpgradeCategory(SuiteId, CheckIn, "Standard зайнято, Suite за ціною Standard", OperatorId, Now);
        booking.CheckIn(RoomId, SuiteId, CheckIn, OperatorId, Now);

        Assert.Equal(SuiteId, booking.RoomCategoryId);
        Assert.Equal(5000m, booking.TotalPrice);
        Assert.All(booking.Nights, n => Assert.Equal(StandardPrice, n.Price));
        var record = booking.History.Single(h => h.FieldName == nameof(Booking.RoomCategoryId));
        Assert.Equal("Standard зайнято, Suite за ціною Standard", record.Comment);
    }

    [Fact]
    public void UpgradeBeforeArrivalDayIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.UpgradeCategory(SuiteId, CheckIn.AddDays(-1), null, OperatorId, Now));
    }

    [Fact]
    public void UpgradeToTheSameCategoryOrWithPreAssignedRoomIsRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);
        Assert.Throws<DomainException>(() => booking.UpgradeCategory(StandardId, CheckIn, null, OperatorId, Now));

        booking.AssignRoom(RoomId, StandardId, CheckIn, OperatorId, Now);
        Assert.Throws<DomainException>(() => booking.UpgradeCategory(SuiteId, CheckIn, null, OperatorId, Now));
    }

    [Fact]
    public void ExtendAddsNightsAtTheGivenPrice()
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        booking.Extend(CheckOut.AddDays(2), Nights(CheckOut, CheckOut.AddDays(2), 2700m), OperatorId, Now);

        Assert.Equal(CheckOut.AddDays(2), booking.CheckOutDate);
        Assert.Equal(4, booking.Nights.Count);
        Assert.Equal(5000m + 5400m, booking.TotalPrice);
    }

    [Fact]
    public void ExtendRejectsEarlierDateAndWrongNights()
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        Assert.Throws<DomainException>(() => booking.Extend(CheckOut, [], OperatorId, Now));
        Assert.Throws<DomainException>(() => booking.Extend(CheckOut.AddDays(2), Nights(CheckOut, CheckOut.AddDays(1)), OperatorId, Now));
        Assert.Equal(CheckOut, booking.CheckOutDate);
    }

    [Fact]
    public void ShortenStayRemovesLaterNights()
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        booking.ShortenStay(CheckIn.AddDays(1), CheckIn, OperatorId, Now);

        Assert.Equal(CheckIn.AddDays(1), booking.CheckOutDate);
        Assert.Equal([CheckIn], booking.Nights.Select(n => n.Night));
        Assert.Equal(StandardPrice, booking.TotalPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void ShortenStayNeedsDateInsideTheStay(int daysAfterCheckIn)
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        Assert.Throws<DomainException>(() => booking.ShortenStay(CheckIn.AddDays(daysAfterCheckIn), CheckIn, OperatorId, Now));
    }

    [Fact]
    public void ShortenStayKeepsTheNightsAlreadyStayed()
    {
        // A four-night stay 10–14.11; on 13.11 the guest has already stayed three nights.
        var booking = InStatus(BookingStatus.CheckedIn);
        booking.Extend(CheckOut.AddDays(2), Nights(CheckOut, CheckOut.AddDays(2)), OperatorId, Now);
        var hotelToday = CheckIn.AddDays(3);
        var historyCount = booking.History.Count;

        Assert.Throws<DomainException>(() => booking.ShortenStay(CheckIn.AddDays(1), hotelToday, OperatorId, Now.AddDays(3)));
        Assert.Equal(4, booking.Nights.Count);
        Assert.Equal(historyCount, booking.History.Count);

        // Leaving today removes only tonight.
        booking.ShortenStay(hotelToday, hotelToday, OperatorId, Now.AddDays(3));
        Assert.Equal(hotelToday, booking.CheckOutDate);
        Assert.Equal(3, booking.Nights.Count);
        Assert.Equal(3 * StandardPrice, booking.TotalPrice);
    }

    [Fact]
    public void MoveToRoomRequiresAnotherRoomOfTheSameCategory()
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        Assert.Throws<DomainException>(() => booking.MoveToRoom(RoomId, StandardId, null, OperatorId, Now));
        Assert.Throws<DomainException>(() => booking.MoveToRoom(OtherRoomId, SuiteId, null, OperatorId, Now));

        booking.MoveToRoom(OtherRoomId, StandardId, "Не працює кондиціонер", OperatorId, Now);
        Assert.Equal(OtherRoomId, booking.RoomId);
        Assert.Equal("Не працює кондиціонер", booking.History.Last().Comment);
    }

    [Fact]
    public void ChangeWishesAndGuestsCountAreRecorded()
    {
        var booking = NewBooking();

        booking.ChangeWishes("Високий поверх", OperatorId, Now);
        booking.ChangeGuestsCount(1, OperatorId, Now);
        booking.ChangeGuestsCount(1, OperatorId, Now); // no change, no record

        var records = booking.History.Skip(1).ToList();
        Assert.Equal(2, records.Count);
        Assert.Equal(("Тихий номер", "Високий поверх"), (records[0].OldValue, records[0].NewValue));
        Assert.Equal(("2", "1"), (records[1].OldValue, records[1].NewValue));
        Assert.Equal(Now, booking.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void RecordPaymentNeedsPositiveAmount(int amount)
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        Assert.Throws<DomainException>(() => booking.RecordPayment(amount, null, OperatorId, Now));
    }

    [Fact]
    public void AmountsFinerThanAKopiykaAreRejected()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        Assert.Throws<DomainException>(() => booking.RecordPayment(0.004m, null, OperatorId, Now));
        Assert.Throws<DomainException>(() => booking.ChangeDates(
            CheckIn, CheckOut.AddDays(1), [.. Nights(CheckIn, CheckOut), new(CheckOut, 2500.005m)], Deadline, OperatorId, Now));
        Assert.Equal(CheckOut, booking.CheckOutDate);
        Assert.Equal(5000m, booking.TotalPrice);
    }

    [Fact]
    public void RecordPaymentWritesPaymentRecordedWithoutChangingTheBooking()
    {
        var booking = InStatus(BookingStatus.Completed);
        var updatedAt = booking.UpdatedAtUtc;

        booking.RecordPayment(5000m, "Оплата боргу переказом", OperatorId, Now.AddDays(10));

        var record = booking.History.Last();
        Assert.Equal(BookingHistoryEventType.PaymentRecorded, record.EventType);
        Assert.Equal("5000.00", record.NewValue);
        Assert.Equal(updatedAt, booking.UpdatedAtUtc);
    }
}
