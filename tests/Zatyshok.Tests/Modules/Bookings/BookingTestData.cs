using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Bookings;

namespace Zatyshok.Tests.Modules.Bookings;

/// <summary>A two-night Standard booking 10–12.11.2026 and helpers that bring it to any state.</summary>
internal static class BookingTestData
{
    public static readonly Guid HotelId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid StandardId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid SuiteId = Guid.Parse("11111111-1111-1111-1111-111111111103");
    /// <summary>The guest profile (guests.Guests) that owns the booking.</summary>
    public static readonly Guid GuestId = Guid.Parse("88888888-8888-8888-8888-888888888801");

    /// <summary>
    /// The guest's account (identity.Users, Guest.UserId): the author of the guest's own actions,
    /// because BookingHistory.AuthorUserId references Users, not Guests (table 12.14).
    /// </summary>
    public static readonly Guid GuestUserId = Guid.Parse("77777777-7777-7777-7777-777777777701");
    public static readonly Guid RoomId = Guid.Parse("33333333-3333-3333-3333-333333330000");
    public static readonly Guid OtherRoomId = Guid.Parse("33333333-3333-3333-3333-333333330001");
    public static readonly Guid OperatorId = Guid.Parse("77777777-7777-7777-7777-777777777702");

    public static readonly DateOnly CheckIn = new(2026, 11, 10);
    public static readonly DateOnly CheckOut = new(2026, 11, 12);

    /// <summary>18:00 Kyiv time on the day before check-in.</summary>
    public static readonly DateTime Deadline = new(2026, 11, 9, 16, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime CreatedAt = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    public const decimal StandardPrice = 2500m;
    public const decimal SuitePrice = 6000m;

    /// <summary>Nights [from; to) at one price.</summary>
    public static List<NightPrice> Nights(DateOnly from, DateOnly to, decimal price = StandardPrice)
    {
        var nights = new List<NightPrice>();
        for (var night = from; night < to; night = night.AddDays(1))
        {
            nights.Add(new NightPrice(night, price));
        }

        return nights;
    }

    public static Booking NewBooking() => new(
        HotelId,
        StandardId,
        GuestId,
        corporateClientId: null,
        CheckIn,
        CheckOut,
        guestsCount: 2,
        BookingSource.Phone,
        wishes: "Тихий номер",
        Nights(CheckIn, CheckOut),
        Deadline,
        OperatorId,
        CreatedAt);

    /// <summary>A booking in the given state, reached through allowed transitions only.</summary>
    public static Booking InStatus(BookingStatus status)
    {
        var booking = NewBooking();
        switch (status)
        {
            case BookingStatus.New:
                break;
            case BookingStatus.Confirmed:
                booking.Confirm(OperatorId, CreatedAt);
                break;
            case BookingStatus.CheckedIn:
                booking.Confirm(OperatorId, CreatedAt);
                booking.CheckIn(RoomId, StandardId, CheckIn, OperatorId, Now);
                break;
            case BookingStatus.Completed:
                booking.Confirm(OperatorId, CreatedAt);
                booking.CheckIn(RoomId, StandardId, CheckIn, OperatorId, Now);
                booking.CheckOut(OperatorId, Now.AddDays(2));
                break;
            case BookingStatus.Cancelled:
                booking.Cancel("Гість передумав", byGuest: false, OperatorId, CreatedAt);
                break;
            case BookingStatus.NoShow:
                booking.Confirm(OperatorId, CreatedAt);
                booking.MarkNoShow(CheckIn.AddDays(1), Now.AddDays(1));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return booking;
    }
}
