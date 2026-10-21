using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Modules.Rooms;

/// <summary>Overlap of seasonal periods of one rate (FR-08): both bounds are nights of the period.</summary>
public class SeasonRateTests
{
    // 1–10 June: the nights from 1 June to 10 June inclusive.
    private static readonly SeasonRate June = new()
    {
        Name = "Червень",
        StartDate = new DateOnly(2027, 6, 1),
        EndDate = new DateOnly(2027, 6, 10),
        PricePerNight = 2000m,
    };

    [Theory]
    [InlineData("2027-05-20", "2027-06-01")] // ends on the first night
    [InlineData("2027-06-10", "2027-06-20")] // starts on the last night
    [InlineData("2027-06-03", "2027-06-05")] // inside
    [InlineData("2027-05-01", "2027-07-01")] // around
    public void PeriodsSharingANightOverlap(string start, string end)
    {
        Assert.True(June.Overlaps(DateOnly.Parse(start), DateOnly.Parse(end)));
    }

    [Theory]
    [InlineData("2027-05-20", "2027-05-31")] // ends the night before
    [InlineData("2027-06-11", "2027-06-20")] // starts the night after
    public void AdjacentPeriodsDoNotOverlap(string start, string end)
    {
        Assert.False(June.Overlaps(DateOnly.Parse(start), DateOnly.Parse(end)));
    }
}
