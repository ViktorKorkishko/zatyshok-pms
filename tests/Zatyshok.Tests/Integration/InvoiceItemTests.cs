using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// Invoice lines (table 12.20): the stored generated column Amount and the rule "an accommodation
/// line is exactly one night, one line per night" (table 12.32). The domain keeps the rule too,
/// so the second line is inserted with plain SQL, as a bug in a later service could do.
/// </summary>
[Trait("Category", "Integration")]
public class InvoiceItemTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task DatabaseComputesAmountOfEveryLine()
    {
        await using var db = database.CreateDbContext();
        var (invoice, service) = await CreateInvoiceAsync(db);
        var line = invoice.AddItem(service.Id, service.Name, invoice.Items.First().ServiceDate, quantity: 3, unitPrice: 350m, null, DateTime.UtcNow);

        // Until the line is saved Amount is 0 in memory; EF reads the computed value back on save.
        Assert.Equal(0m, line.Amount);
        await db.SaveChangesAsync();
        Assert.Equal(1050m, line.Amount);

        await using var check = database.CreateDbContext();
        var lines = await check.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).ToListAsync();

        Assert.All(lines, l => Assert.Equal(l.Quantity * l.UnitPrice, l.Amount));
        Assert.Equal(new[] { 1050m, TestData.Price, TestData.Price }, lines.Select(l => l.Amount).Order());
    }

    [Fact]
    public async Task SecondAccommodationLineForTheSameNightIsRejected()
    {
        await using var db = database.CreateDbContext();
        var (invoice, service) = await CreateInvoiceAsync(db);
        var night = invoice.Items.First().ServiceDate;

        await using var connection = await database.OpenConnectionAsync();
        var error = await PostgresAssert.SqlFailsAsync(connection, InsertLineSql(invoice.Id, "Accommodation", null, night, 1), PostgresErrorCodes.UniqueViolation);
        Assert.Equal("UX_InvoiceItems_InvoiceId_ServiceDate", error.ConstraintName);

        // The index is partial: a service line on the same date is fine.
        await using var command = new NpgsqlCommand(InsertLineSql(invoice.Id, "Service", service.Id, night, 1), connection);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task AccommodationLineForSeveralNightsIsRejected()
    {
        await using var db = database.CreateDbContext();
        var (invoice, _) = await CreateInvoiceAsync(db);
        var nextFreeNight = invoice.Items.Max(i => i.ServiceDate).AddDays(1);

        await using var connection = await database.OpenConnectionAsync();
        var error = await PostgresAssert.SqlFailsAsync(connection, InsertLineSql(invoice.Id, "Accommodation", null, nextFreeNight, 2), PostgresErrorCodes.CheckViolation);
        Assert.Equal("CK_InvoiceItems_OneNightPerLine", error.ConstraintName);
    }

    /// <summary>A two-night booking with its invoice (two accommodation lines) and a catalog service.</summary>
    private static async Task<(Invoice Invoice, Service Service)> CreateInvoiceAsync(ZatyshokDbContext db)
    {
        var hotel = await TestData.CreateHotelAsync(db);
        var booking = TestData.NewBooking(hotel, hotel.Today.AddDays(5), nights: 2);
        var invoice = new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow);
        var service = new Service { Name = $"Сніданок {TestData.Unique()}", Price = 350m };

        db.Bookings.Add(booking);
        db.Invoices.Add(invoice);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return (invoice, service);
    }

    private static string InsertLineSql(Guid invoiceId, string kind, Guid? serviceId, DateOnly date, int quantity)
    {
        var service = serviceId is null ? "NULL" : $"'{serviceId}'";
        return $"""
            INSERT INTO billing."InvoiceItems" ("Id", "InvoiceId", "Kind", "ServiceId", "Description", "ServiceDate", "Quantity", "UnitPrice", "CreatedAtUtc")
            VALUES ('{Guid.NewGuid()}', '{invoiceId}', '{kind}', {service}, 'Рядок з SQL', '{date:yyyy-MM-dd}', {quantity}, 100, now())
            """;
    }
}
