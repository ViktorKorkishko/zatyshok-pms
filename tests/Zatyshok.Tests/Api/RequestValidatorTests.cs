using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Api.Modules.Rooms;

namespace Zatyshok.Tests.Api;

/// <summary>Rules of the request validators at their boundaries; the HTTP side is in Integration/Api.</summary>
public class RequestValidatorTests
{
    private static HotelRequest ValidHotel() => new()
    {
        Name = "Затишок Ужгород",
        City = "Ужгород",
        Address = "вул. Корзо, 7",
        Stars = 3,
        TimeZoneId = "Europe/Kyiv",
    };

    private static List<string> FailedFields<T>(IValidator<T> validator, T request)
        => validator.Validate(request).Errors.Select(e => e.PropertyName).ToList();

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1_000_000, 100, true)]
    [InlineData(1_000_001, 100, false)]
    public void PageIsFrom1To1000000AndHoldsAtMost100Rows(int page, int pageSize, bool valid)
    {
        var result = new PageQueryValidator().Validate(new PageQuery { Page = page, PageSize = pageSize });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void ListQueryIncludesThePageRules()
    {
        Assert.Equal(["PageSize"], FailedFields(new HotelListQueryValidator(), new HotelListQuery { PageSize = 500 }));
    }

    [Fact]
    public void HotelWithOnlyRequiredFieldsIsValid()
    {
        Assert.Empty(FailedFields(new HotelRequestValidator(), ValidHotel()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void StarsAreFrom1To5(int stars, bool valid)
    {
        var result = new HotelRequestValidator().Validate(new HotelRequest
        {
            Name = "Х", City = "Х", Address = "Х", TimeZoneId = "Europe/Kyiv", Stars = stars,
        });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(365, true)]
    [InlineData(-1, false)]
    [InlineData(366, false)]
    public void CancellationDeadlineIsFrom0To365DaysBeforeCheckIn(int days, bool valid)
    {
        var request = new HotelRequest
        {
            Name = "Х", City = "Х", Address = "Х", Stars = 3, TimeZoneId = "Europe/Kyiv", CancellationDeadlineDaysBefore = days,
        };

        Assert.Equal(valid, new HotelRequestValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("Europe/Kyiv", true)]
    [InlineData(" Europe/Kyiv ", true)] // the service trims it before saving
    [InlineData("UTC", true)]
    [InlineData("Europe/Atlantis", false)]
    [InlineData("", false)]
    public void TimeZoneMustBeKnownToTheServer(string timeZoneId, bool valid)
    {
        var request = new HotelRequest { Name = "Х", City = "Х", Address = "Х", Stars = 3, TimeZoneId = timeZoneId };

        var errors = new HotelRequestValidator().Validate(request).Errors;

        Assert.Equal(valid, errors.Count == 0);
        Assert.True(errors.Count <= 1, "one message per field");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("kyiv@zatyshok.ua", true)]
    [InlineData("kyiv.zatyshok.ua", false)]
    public void EmailIsOptionalButMustBeAnAddress(string? email, bool valid)
    {
        var request = new HotelRequest { Name = "Х", City = "Х", Address = "Х", Stars = 3, TimeZoneId = "Europe/Kyiv", Email = email };

        Assert.Equal(valid, new HotelRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void OmittedHotelRulesTakeTheDefaults()
    {
        var data = ValidHotel().ToData();

        Assert.Equal(1, data.CancellationDeadlineDaysBefore);
        Assert.Equal(new TimeOnly(18, 0), data.CancellationDeadlineTime);
        Assert.Equal(new TimeOnly(14, 0), data.CheckInTime);
        Assert.Equal(new TimeOnly(12, 0), data.CheckOutTime);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void CategoryCapacityIsPositive(int capacity, bool valid)
    {
        var request = new RoomCategoryRequest { Name = "Family", Capacity = capacity };

        Assert.Equal(valid, new RoomCategoryRequestValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("0.01", true)]
    [InlineData("9999999999.99", true)]
    [InlineData("-0.01", false)]
    [InlineData("0.001", false)]
    [InlineData("10000000000.00", false)]
    public void ServicePriceIsNotNegativeAndFitsTheMoneyColumn(string price, bool valid)
    {
        var request = new ServiceRequest { Name = "Пральня", Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture) };

        Assert.Equal(valid, new ServiceRequestValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("Name", "name")]
    [InlineData("CheckInDate", "checkInDate")]
    [InlineData("Items[0].UnitPrice", "items[0].unitPrice")]
    [InlineData("$.stars", "$.stars")]
    public void ErrorKeysAreTheJsonNamesOfTheFields(string key, string expected)
    {
        Assert.Equal(expected, ValidationProblems.ToJsonName(key));
    }
}
