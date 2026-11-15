using Zatyshok.Api.Modules.Bookings;

namespace Zatyshok.Tests.Api;

/// <summary>
/// Rules of the availability search that need no data (FR-10). The check-in against the hotel's current date
/// and the guests against the capacity of a category are checked by the service (Integration/Api).
/// </summary>
public class AvailabilityQueryValidatorTests
{
    private static readonly Guid Hotel = Guid.Parse("22222222-2222-2222-2222-222222222203");
    private static readonly DateOnly CheckIn = new(2027, 5, 30);

    private static AvailabilityQuery Valid() => new()
    {
        HotelId = Hotel,
        CheckIn = CheckIn,
        CheckOut = CheckIn.AddDays(3),
        Guests = 2,
    };

    private static Dictionary<string, string> Errors(AvailabilityQuery query)
        => new AvailabilityQueryValidator().Validate(query).Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);

    [Fact]
    public void HotelDatesAndGuestsAreEnough()
    {
        Assert.Empty(Errors(Valid()));
    }

    [Fact]
    public void CityInsteadOfHotelIsEnough()
    {
        Assert.Empty(Errors(new AvailabilityQuery { City = "Одеса", CheckIn = CheckIn, CheckOut = CheckIn.AddDays(1), Guests = 1 }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void HotelOrCityIsRequired(string? city)
    {
        var errors = Errors(new AvailabilityQuery { City = city, CheckIn = CheckIn, CheckOut = CheckIn.AddDays(1), Guests = 1 });

        Assert.Equal("Вкажіть готель або місто.", Assert.Single(errors).Value);
        Assert.Equal("HotelId", errors.Keys.Single());
    }

    [Fact]
    public void HotelAndCityTogetherAreRejected()
    {
        var errors = Errors(new AvailabilityQuery { HotelId = Hotel, City = "Одеса", CheckIn = CheckIn, CheckOut = CheckIn.AddDays(1), Guests = 1 });

        Assert.Equal("Вкажіть або готель, або місто, а не обидва.", errors["HotelId"]);
    }

    [Fact]
    public void CityIsAtMost100Characters()
    {
        var errors = Errors(new AvailabilityQuery { City = new string('х', 101), CheckIn = CheckIn, CheckOut = CheckIn.AddDays(1), Guests = 1 });

        Assert.Equal("Назва міста — до 100 символів.", errors["City"]);
    }

    [Fact]
    public void DatesAndGuestsAreRequired()
    {
        var errors = Errors(new AvailabilityQuery { HotelId = Hotel });

        Assert.Equal(["CheckIn", "CheckOut", "Guests"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Вкажіть дату заїзду.", errors["CheckIn"]);
        Assert.Equal("Вкажіть дату виїзду.", errors["CheckOut"]);
        Assert.Equal("Вкажіть кількість гостей.", errors["Guests"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CheckOutMustBeLaterThanCheckIn(int nights)
    {
        var errors = Errors(new AvailabilityQuery { HotelId = Hotel, CheckIn = CheckIn, CheckOut = CheckIn.AddDays(nights), Guests = 1 });

        Assert.Equal("Дата виїзду має бути пізнішою за дату заїзду.", Assert.Single(errors).Value);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void StayIsAtMost30Nights(int nights, bool valid)
    {
        var errors = Errors(new AvailabilityQuery { HotelId = Hotel, CheckIn = CheckIn, CheckOut = CheckIn.AddDays(nights), Guests = 1 });

        Assert.Equal(valid, errors.Count == 0);
        if (!valid)
        {
            Assert.Equal("Період проживання — не більше 30 ночей.", errors["CheckOut"]);
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, true)] // the capacity of a category is checked by the service, which knows the categories
    [InlineData(0, false)]
    [InlineData(-2, false)]
    public void AtLeastOneGuest(int guests, bool valid)
    {
        var query = Valid();
        var errors = Errors(new AvailabilityQuery { HotelId = query.HotelId, CheckIn = query.CheckIn, CheckOut = query.CheckOut, Guests = guests });

        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void BlankCityGoesToTheServiceAsNoCity()
    {
        var search = new AvailabilityQuery { HotelId = Hotel, City = " ", CheckIn = CheckIn, CheckOut = CheckIn.AddDays(2), Guests = 2 }.ToSearch();

        Assert.Equal(Hotel, search.HotelId);
        Assert.Null(search.City);
        Assert.Equal((CheckIn, CheckIn.AddDays(2), 2), (search.CheckIn, search.CheckOut, search.Guests));
    }
}
