using System.Data;
using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// Two payments to the same invoice at the same time (table 12.28, listing 12.2). A payment that
/// keeps the invoice status inserts a Payments row but does not update the invoice row, so the
/// xmin token is not checked and cannot catch the conflict. The payments are serialized by locking
/// the invoice row, as the T6 invoice service will do; the test plays its steps.
/// </summary>
[Trait("Category", "Integration")]
public class InvoiceLockConcurrencyTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task SecondPaymentUnderTheInvoiceLockSeesTheFirstOne()
    {
        var (invoiceId, staffId) = await CreateInvoiceAsync();

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();

        // 1. The first transaction locks the invoice and loads it.
        await using var firstTransaction = await first.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var firstInvoice = await LockAndLoadAsync(first, invoiceId);

        // 2. The second transaction asks for the same lock in the background and has to wait.
        await using var secondTransaction = await second.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var secondPid = await RowLocks.GetBackendPidAsync(second);
        var secondAttempt = Task.Run(() => LockAndLoadAsync(second, invoiceId));
        await RowLocks.WaitUntilWaitingForLockAsync(database, secondPid);

        // 3. The first pays 3000 of the remaining 4000 and commits; this releases the lock.
        AddPayment(firstInvoice, 3000m, staffId);
        await first.SaveChangesAsync();
        await firstTransaction.CommitAsync();

        // 4. The second loads the invoice only now and sees that payment: 1000 is left, so the
        //    same payment of 3000 is rejected (UC-18 A1) instead of overpaying the invoice.
        var secondInvoice = await secondAttempt;
        Assert.Equal(4000m, secondInvoice.GetPaidAmount());
        Assert.Throws<DomainException>(() => AddPayment(secondInvoice, 3000m, staffId));
    }

    /// <summary>
    /// Why the lock is a statement of its own: Items and Payments are auto-included, so a FromSql
    /// lock query that returns the invoice (listing П.3) loads them in the same statement. Under
    /// READ COMMITTED that statement sees the data as of its start, before the wait, and the payment
    /// committed meanwhile is missing. The second payment would then pass and overpay the invoice.
    /// </summary>
    [Fact]
    public async Task LockQueryThatLoadsTheInvoiceSeesPaymentsAsBeforeTheWait()
    {
        var (invoiceId, staffId) = await CreateInvoiceAsync();

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();

        await using var firstTransaction = await first.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var firstInvoice = await LockWithFromSqlAsync(first, invoiceId);

        await using var secondTransaction = await second.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var secondPid = await RowLocks.GetBackendPidAsync(second);
        var secondAttempt = Task.Run(() => LockWithFromSqlAsync(second, invoiceId));
        await RowLocks.WaitUntilWaitingForLockAsync(database, secondPid);

        AddPayment(firstInvoice, 3000m, staffId);
        await first.SaveChangesAsync();
        await firstTransaction.CommitAsync();

        var secondInvoice = await secondAttempt;
        Assert.Equal(1000m, secondInvoice.GetPaidAmount());
    }

    /// <summary>
    /// The lock of listing 12.2 as its own statement, then the invoice with its lines and payments.
    /// The second statement starts after the lock is granted, so it sees every committed payment.
    /// </summary>
    private static async Task<Invoice> LockAndLoadAsync(ZatyshokDbContext db, Guid invoiceId)
    {
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM billing."Invoices" WHERE "Id" = {invoiceId} FOR UPDATE""");
        return await db.Invoices.SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>The lock of listing П.3: one statement that locks and returns the invoice (EF adds the auto-included collections).</summary>
    private static async Task<Invoice> LockWithFromSqlAsync(ZatyshokDbContext db, Guid invoiceId)
        => (await db.Invoices
                .FromSql($"""SELECT xmin, * FROM billing."Invoices" WHERE "Id" = {invoiceId} FOR UPDATE""")
                .ToListAsync())
            .Single();

    private static void AddPayment(Invoice invoice, decimal amount, Guid staffId)
        => invoice.AddPayment(PaymentKind.Payment, PaymentMethod.Card, amount, DateTime.UtcNow, null, staffId, DateTime.UtcNow);

    /// <summary>An invoice for two nights at 2500 (T = 5000) with a prepayment of 1000.</summary>
    private async Task<(Guid InvoiceId, Guid StaffId)> CreateInvoiceAsync()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);
        var staff = await TestData.CreateStaffAsync(db);
        var booking = TestData.NewBooking(hotel, hotel.Today.AddDays(5), nights: 2);
        var invoice = new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow);
        AddPayment(invoice, 1000m, staff.Id);

        db.Bookings.Add(booking);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return (invoice.Id, staff.Id);
    }
}
