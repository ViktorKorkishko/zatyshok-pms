using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Modules.Rooms;

/// <summary>Hotel calendar rules: the hotel's date (NFR-20) and the free cancellation deadline (FR-15, 12.3.3).</summary>
public class HotelTests
{
    private static Hotel Kyiv() => new()
    {
        Name = "Затишок Київ",
        City = "Київ",
        Address = "вул. Хрещатик, 1",
        TimeZoneId = "Europe/Kyiv",
    };

    [Fact]
    public void NewHotelHasDefaultRulesOf12_3_2()
    {
        var hotel = Kyiv();

        Assert.Equal(1, hotel.CancellationDeadlineDaysBefore);
        Assert.Equal(new TimeOnly(18, 0), hotel.CancellationDeadlineTime);
        Assert.Equal(new TimeOnly(14, 0), hotel.CheckInTime);
        Assert.Equal(new TimeOnly(12, 0), hotel.CheckOutTime);
    }

    [Theory]
    [InlineData("2026-10-20T20:59:00Z", "2026-10-20")] // 23:59 in Kyiv (UTC+3, summer time)
    [InlineData("2026-10-20T21:00:00Z", "2026-10-21")] // midnight in Kyiv
    [InlineData("2026-11-10T22:30:00Z", "2026-11-11")] // 00:30 in Kyiv (UTC+2, winter time)
    public void LocalDateIsTheCalendarDateOfTheHotelTimeZone(string utcNow, string expected)
    {
        var now = DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(DateOnly.Parse(expected), Kyiv().GetLocalDate(now));
    }

    [Theory]
    [InlineData("2026-11-10", "2026-11-09T16:00:00Z")] // 18:00 Kyiv winter time (UTC+2)
    [InlineData("2027-07-10", "2027-07-09T15:00:00Z")] // 18:00 Kyiv summer time (UTC+3)
    [InlineData("2027-03-29", "2027-03-28T15:00:00Z")] // the day of the spring clock change
    public void DefaultDeadlineIs18OfTheDayBeforeCheckIn(string checkIn, string expectedUtc)
    {
        var deadline = Kyiv().GetFreeCancellationUntilUtc(DateOnly.Parse(checkIn));

        Assert.Equal(DateTimeKind.Utc, deadline.Kind);
        Assert.Equal(DateTime.Parse(expectedUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal), deadline);
    }

    [Fact]
    public void DeadlineFollowsHotelSettings()
    {
        var hotel = Kyiv();
        hotel.CancellationDeadlineDaysBefore = 3;
        hotel.CancellationDeadlineTime = new TimeOnly(12, 0);

        var deadline = hotel.GetFreeCancellationUntilUtc(new DateOnly(2026, 11, 10));

        Assert.Equal(new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc), deadline);
    }

    [Fact]
    public void DeadlineInsideTheSkippedSpringHourMovesToTheNextHour()
    {
        var hotel = Kyiv();
        hotel.CancellationDeadlineTime = new TimeOnly(3, 30); // 03:00–04:00 does not exist on 2027-03-28

        var deadline = hotel.GetFreeCancellationUntilUtc(new DateOnly(2027, 3, 29));

        Assert.Equal(new DateTime(2027, 3, 28, 1, 30, 0, DateTimeKind.Utc), deadline);
    }
}
