using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Modules.Rooms;

/// <summary>
/// Cost of a stay with seasonal prices (FR-08): the sum of the prices of the nights check-in … check-out − 1,
/// where a seasonal period (bounds inclusive, table 12.1) takes priority over the base price.
/// </summary>
public class RatePricingTests
{
    private const decimal Base = 1500m;
    private const decimal Winter = 2100m;

    // The winter holidays of the seed in Yaremche: the nights from 20 December to 10 January inclusive.
    private static Rate Karpaty()
    {
        var rate = new Rate { BasePricePerNight = Base };
        rate.SeasonRates.Add(Season("Зимові свята", "2026-12-20", "2027-01-10", Winter));
        return rate;
    }

    [Fact]
    public void WithoutSeasonsEveryNightCostsTheBasePrice()
    {
        var rate = new Rate { BasePricePerNight = 2500m };

        var nights = rate.GetNightPrices(D("2026-11-10"), D("2026-11-13"));

        Assert.Equal([D("2026-11-10"), D("2026-11-11"), D("2026-11-12")], nights.Select(n => n.Night));
        Assert.All(nights, n => Assert.Equal(2500m, n.Price));
        Assert.Equal(7500m, nights.Sum(n => n.Price));
    }

    [Fact]
    public void StayAcrossTheStartOfASeasonPaysBothPrices()
    {
        // Nights 18 and 19 December before the season, 20 and 21 in it.
        var nights = Karpaty().GetNightPrices(D("2026-12-18"), D("2026-12-22"));

        Assert.Equal([Base, Base, Winter, Winter], nights.Select(n => n.Price));
        Assert.Equal(7200m, nights.Sum(n => n.Price));
    }

    [Fact]
    public void StayAcrossTheEndOfASeasonPaysTheSeasonPriceForItsLastNight()
    {
        // 10 January is the last night of the period (inclusive bound), 11 January is not.
        var nights = Karpaty().GetNightPrices(D("2027-01-09"), D("2027-01-12"));

        Assert.Equal(
            [new NightPrice(D("2027-01-09"), Winter), new NightPrice(D("2027-01-10"), Winter), new NightPrice(D("2027-01-11"), Base)],
            nights);
        Assert.Equal(5700m, nights.Sum(n => n.Price));
    }

    [Fact]
    public void CheckOutOnTheFirstDayOfASeasonIsNotASeasonNight()
    {
        // The check-out day is not a night: leaving on 20 December, the guest pays no winter price.
        var nights = Karpaty().GetNightPrices(D("2026-12-18"), D("2026-12-20"));

        Assert.All(nights, n => Assert.Equal(Base, n.Price));
    }

    [Fact]
    public void CheckInOnTheDayAfterASeasonPaysTheBasePrice()
    {
        var nights = Karpaty().GetNightPrices(D("2027-01-11"), D("2027-01-13"));

        Assert.All(nights, n => Assert.Equal(Base, n.Price));
    }

    [Fact]
    public void StayAroundAShortSeasonPaysTheSeasonPriceOnlyInside()
    {
        var rate = new Rate { BasePricePerNight = Base };
        rate.SeasonRates.Add(Season("Фестиваль", "2027-03-05", "2027-03-06", 3000m));

        var nights = rate.GetNightPrices(D("2027-03-04"), D("2027-03-08"));

        Assert.Equal([Base, 3000m, 3000m, Base], nights.Select(n => n.Price));
    }

    [Fact]
    public void AdjacentSeasonsEachPriceTheirOwnNights()
    {
        var rate = new Rate { BasePricePerNight = Base };
        rate.SeasonRates.Add(Season("Грудень", "2026-12-01", "2026-12-31", 2000m));
        rate.SeasonRates.Add(Season("Січень", "2027-01-01", "2027-01-31", 1800m));

        var nights = rate.GetNightPrices(D("2026-12-30"), D("2027-01-02"));

        Assert.Equal([2000m, 2000m, 1800m], nights.Select(n => n.Price));
    }

    [Fact]
    public void OverlappingSeasonsWrittenPastTheServiceGiveTheLaterStartedPeriod()
    {
        // The API rejects overlapping periods (SeasonRateService); if rows overlap anyway, the price
        // still does not depend on the order of the rows.
        var first = new Rate { BasePricePerNight = Base };
        first.SeasonRates.Add(Season("Довгий", "2027-07-01", "2027-07-31", 2000m));
        first.SeasonRates.Add(Season("Короткий", "2027-07-10", "2027-07-12", 2600m));
        var second = new Rate { BasePricePerNight = Base };
        foreach (var season in first.SeasonRates.Reverse())
        {
            second.SeasonRates.Add(season);
        }

        Assert.Equal(2600m, first.GetPrice(D("2027-07-11")));
        Assert.Equal(2600m, second.GetPrice(D("2027-07-11")));
        Assert.Equal(2000m, first.GetPrice(D("2027-07-13")));
    }

    [Fact]
    public void CheckOutNotAfterCheckInIsAProgrammingError()
    {
        // The API rejects such dates with 400 before any price is computed.
        Assert.Throws<ArgumentException>(() => Karpaty().GetNightPrices(D("2026-12-18"), D("2026-12-18")));
    }

    private static SeasonRate Season(string name, string start, string end, decimal price)
        => new() { Name = name, StartDate = D(start), EndDate = D(end), PricePerNight = price };

    private static DateOnly D(string date) => DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
}
