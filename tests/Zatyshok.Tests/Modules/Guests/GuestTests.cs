using Zatyshok.Domain.Modules.Guests;

namespace Zatyshok.Tests.Modules.Guests;

public class GuestTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData(DocumentType.Passport, null, false)]
    [InlineData(DocumentType.Passport, " ", false)]
    [InlineData(DocumentType.IdCard, "001234567", true)]
    public void HasDocumentNeedsTypeAndNumber(DocumentType? type, string? number, bool expected)
    {
        var guest = new Guest { LastName = "Шевченко", FirstName = "Тарас", DocumentType = type, DocumentNumber = number };

        Assert.Equal(expected, guest.HasDocument());
    }
}
