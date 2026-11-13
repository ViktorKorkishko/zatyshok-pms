using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// The invariants of Booking (nights and TotalPrice) and Invoice (status from the sums) are kept by
/// methods that read the child collections. A service that loads the aggregate with a plain query,
/// without Include, must still get the collections, otherwise the method works with an empty list
/// and saves inconsistent rows without any error. The configurations therefore
/// load Booking.Nights, Invoice.Items and Invoice.Payments automatically (AutoInclude).
/// </summary>
[Trait("Category", "Integration")]
public class AggregateLoadingTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task ChangeDatesOnBookingReadByPlainQueryReplacesItsNights()
    {
        Guid bookingId;
        DateOnly newCheckIn;
        await using (var db = database.CreateDbContext())
        {
            var hotel = await TestData.CreateHotelAsync(db);
            var booking = TestData.NewBooking(hotel, hotel.Today.AddDays(5), nights: 2);
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
            newCheckIn = hotel.Today.AddDays(20);
        }

        await using (var db = database.CreateDbContext())
        {
            // No Include: the way a T5 service is most likely to read it.
            var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
            Assert.Equal(2, booking.Nights.Count);

            booking.ChangeDates(newCheckIn, newCheckIn.AddDays(2), TestData.Nights(newCheckIn, 2, 2000m), booking.FreeCancellationUntilUtc, null, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var check = database.CreateDbContext();
        var nights = await check.BookingNights.Where(n => n.BookingId == bookingId).OrderBy(n => n.Night).Select(n => n.Night).ToListAsync();
        var saved = await check.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);

        Assert.Equal([newCheckIn, newCheckIn.AddDays(1)], nights);
        Assert.Equal(4000m, saved.TotalPrice);
    }

    [Fact]
    public async Task AddItemOnInvoiceReadByPlainQueryKeepsItsPaymentState()
    {
        Guid invoiceId;
        Service service;
        await using (var db = database.CreateDbContext())
        {
            var hotel = await TestData.CreateHotelAsync(db);
            var booking = TestData.NewBooking(hotel, hotel.Today.AddDays(5), nights: 2);
            var staff = await TestData.CreateStaffAsync(db);
            service = new Service { Name = $"Сніданок {TestData.Unique()}", Price = 350m };
            var invoice = new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow);
            invoice.AddPayment(PaymentKind.Payment, PaymentMethod.Card, 2000m, DateTime.UtcNow, null, staff.Id, DateTime.UtcNow);
            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);

            db.Bookings.Add(booking);
            db.Services.Add(service);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        await using (var db = database.CreateDbContext())
        {
            var invoice = await db.Invoices.SingleAsync(i => i.Id == invoiceId);
            Assert.Equal((2, 1), (invoice.Items.Count, invoice.Payments.Count));

            invoice.AddItem(service.Id, service.Name, invoice.Items.First().ServiceDate, 1, service.Price, null, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var check = database.CreateDbContext();
        var saved = await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);

        Assert.Equal(InvoiceStatus.PartiallyPaid, saved.Status);
        Assert.Equal((5350m, 2000m), (saved.GetTotal(), saved.GetPaidAmount()));
    }

    [Fact]
    public async Task ListQueryCanSkipTheCollections()
    {
        await using (var db = database.CreateDbContext())
        {
            var hotel = await TestData.CreateHotelAsync(db);
            db.Bookings.Add(TestData.NewBooking(hotel, hotel.Today.AddDays(5), nights: 2));
            await db.SaveChangesAsync();
        }

        // Lists (FR-18) do not need the nights: IgnoreAutoIncludes or a projection with Select.
        await using var check = database.CreateDbContext();
        var bookings = await check.Bookings.AsNoTracking().IgnoreAutoIncludes().ToListAsync();

        Assert.NotEmpty(bookings);
        Assert.All(bookings, b => Assert.Empty(b.Nights));
    }
}
