using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Billing;

namespace Zatyshok.Tests.Modules.Billing;

/// <summary>Invoice state derived from the sums (table 12.28), payments, voiding and the accommodation lines (12.3.6).</summary>
public class InvoiceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly CheckIn = new(2026, 11, 10);
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid BreakfastId = Guid.NewGuid();
    private static readonly Guid ReceptionistId = Guid.NewGuid();
    private static readonly Guid AccountantId = Guid.NewGuid();

    // Two nights at 2500: T = 5000.
    private static List<NightPrice> Nights(int count = 2, decimal price = 2500m)
        => Enumerable.Range(0, count).Select(i => new NightPrice(CheckIn.AddDays(i), price)).ToList();

    private static Invoice NewInvoice() => new(BookingId, null, Nights(), Now);

    private static Payment Pay(Invoice invoice, decimal amount, PaymentKind kind = PaymentKind.Payment)
        => invoice.AddPayment(kind, PaymentMethod.Card, amount, Now, null, ReceptionistId, Now);

    [Fact]
    public void NewInvoiceIsOpenWithOneAccommodationLinePerNight()
    {
        var invoice = new Invoice(BookingId, null, [new(CheckIn, 2100m), new(CheckIn.AddDays(1), 2500m)], Now);

        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(Now, invoice.OpenedAtUtc);
        Assert.All(invoice.Items, i =>
        {
            Assert.Equal(InvoiceItemKind.Accommodation, i.Kind);
            Assert.Equal(1, i.Quantity);
            Assert.Null(i.ServiceId);
            Assert.Null(i.CreatedByUserId);
            Assert.Equal(Invoice.AccommodationDescription, i.Description);
        });
        Assert.Equal(new[] { (CheckIn, 2100m), (CheckIn.AddDays(1), 2500m) }, invoice.Items.Select(i => (i.ServiceDate, i.UnitPrice)));
        Assert.Equal(4600m, invoice.GetTotal());
    }

    [Fact]
    public void InvoiceNeedsAtLeastOneNightAndNoDuplicates()
    {
        Assert.Throws<DomainException>(() => new Invoice(BookingId, null, [], Now));
        Assert.Throws<DomainException>(() => new Invoice(BookingId, null, [new(CheckIn, 2500m), new(CheckIn, 2500m)], Now));
        Assert.Throws<DomainException>(() => new Invoice(BookingId, null, [new(CheckIn, -1m)], Now));
    }

    // Payments as signed amounts: positive — Payment, negative — Refund. T = 5000.
    [Theory]
    [InlineData(new double[0], InvoiceStatus.Open)]
    [InlineData(new[] { 1000.0 }, InvoiceStatus.PartiallyPaid)]
    [InlineData(new[] { 4999.99 }, InvoiceStatus.PartiallyPaid)]
    [InlineData(new[] { 5000.0 }, InvoiceStatus.Paid)]
    [InlineData(new[] { 3000.0, 2000.0 }, InvoiceStatus.Paid)]
    [InlineData(new[] { 5000.0, -1000.0 }, InvoiceStatus.PartiallyPaid)]
    [InlineData(new[] { 2000.0, -2000.0 }, InvoiceStatus.Open)]
    public void StatusIsDerivedFromTheSums(double[] payments, InvoiceStatus expected)
    {
        var invoice = NewInvoice();

        foreach (var amount in payments.Select(p => (decimal)p))
        {
            Pay(invoice, Math.Abs(amount), amount > 0 ? PaymentKind.Payment : PaymentKind.Refund);
        }

        Assert.Equal(expected, invoice.Status);
    }

    [Fact]
    public void PaymentAboveBalanceIsRejected()
    {
        var invoice = NewInvoice();
        Pay(invoice, 4000m);

        Assert.Throws<DomainException>(() => Pay(invoice, 1000.01m));
        Assert.Equal(1000m, invoice.GetBalance());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PaymentAmountMustBePositive(int amount)
    {
        var invoice = NewInvoice();

        Assert.Throws<DomainException>(() => Pay(invoice, amount));
    }

    [Fact]
    public void RefundAbovePaidAmountIsRejected()
    {
        var invoice = NewInvoice();
        Pay(invoice, 1000m);

        Assert.Throws<DomainException>(() => Pay(invoice, 1000.01m, PaymentKind.Refund));
    }

    [Fact]
    public void AddingServiceToPaidInvoiceMakesItPartiallyPaid()
    {
        var invoice = NewInvoice();
        Pay(invoice, 5000m);

        invoice.AddItem(BreakfastId, "Сніданок", CheckIn.AddDays(1), 2, 350m, ReceptionistId, Now);

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(5700m, invoice.GetTotal()); // Amount of the new line is still 0 until it is saved
        Assert.Equal(700m, invoice.GetBalance());
    }

    [Fact]
    public void VoidingPaymentOfPaidInvoiceMakesItPartiallyPaid()
    {
        var invoice = NewInvoice();
        Pay(invoice, 3000m);
        var second = Pay(invoice, 2000m);

        invoice.VoidPayment(second.Id, "Помилкова сума", AccountantId, Now.AddHours(1));

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.True(second.IsVoided);
        Assert.Equal(AccountantId, second.VoidedByUserId);
        Assert.Equal("Помилкова сума", second.VoidReason);
    }

    [Fact]
    public void VoidingTheOnlyPaymentMakesInvoiceOpen()
    {
        var invoice = NewInvoice();
        var payment = Pay(invoice, 1000m);

        invoice.VoidPayment(payment.Id, "Платіж не надійшов", AccountantId, Now);

        Assert.Equal(InvoiceStatus.Open, invoice.Status);
    }

    [Fact]
    public void PaymentIsVoidedOnceAndWithReason()
    {
        var invoice = NewInvoice();
        var payment = Pay(invoice, 1000m);

        Assert.Throws<DomainException>(() => invoice.VoidPayment(payment.Id, " ", AccountantId, Now));
        invoice.VoidPayment(payment.Id, "Дубль", AccountantId, Now);
        Assert.Throws<DomainException>(() => invoice.VoidPayment(payment.Id, "Дубль", AccountantId, Now));
        Assert.Throws<DomainException>(() => invoice.VoidPayment(Guid.NewGuid(), "Дубль", AccountantId, Now));
    }

    [Fact]
    public void PaymentCannotBeVoidedWhileItsRefundIsActive()
    {
        var invoice = NewInvoice();
        var payment = Pay(invoice, 1000m);
        var refund = Pay(invoice, 400m, PaymentKind.Refund);

        Assert.Throws<DomainException>(() => invoice.VoidPayment(payment.Id, "Помилка", AccountantId, Now));

        invoice.VoidPayment(refund.Id, "Помилка", AccountantId, Now);
        invoice.VoidPayment(payment.Id, "Помилка", AccountantId, Now);
        Assert.Equal(0m, invoice.GetPaidAmount());
    }

    [Fact]
    public void RemovingServiceFromPaidInvoiceLeavesOverpaymentForRefund()
    {
        var invoice = NewInvoice();
        var breakfast = invoice.AddItem(BreakfastId, "Сніданок", CheckIn, 1, 350m, ReceptionistId, Now);
        Pay(invoice, 5350m);

        invoice.RemoveItem(breakfast.Id);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status); // P ≥ T
        Assert.Equal(-350m, invoice.GetBalance());
        Pay(invoice, 350m, PaymentKind.Refund);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.GetBalance());
    }

    [Fact]
    public void AccommodationLineCannotBeRemovedDirectly()
    {
        var invoice = NewInvoice();

        Assert.Throws<DomainException>(() => invoice.RemoveItem(invoice.Items.First().Id));
        Assert.Throws<DomainException>(() => invoice.RemoveItem(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, -1)]
    public void ServiceLineNeedsPositiveQuantityAndNonNegativePrice(int quantity, int price)
    {
        var invoice = NewInvoice();

        Assert.Throws<DomainException>(() => invoice.AddItem(BreakfastId, "Сніданок", CheckIn, quantity, price, ReceptionistId, Now));
    }

    [Fact]
    public void SyncAddsRemovesAndRepricesAccommodationLinesOnly()
    {
        var invoice = NewInvoice();
        var breakfast = invoice.AddItem(BreakfastId, "Сніданок", CheckIn, 1, 350m, ReceptionistId, Now);
        var secondNight = invoice.Items.Single(i => i.ServiceDate == CheckIn.AddDays(1));

        // The stay moves one night later and the second night gets a new price.
        invoice.SyncAccommodation([new(CheckIn.AddDays(1), 2700m), new(CheckIn.AddDays(2), 2700m)], Now);

        var accommodation = invoice.Items.Where(i => i.Kind == InvoiceItemKind.Accommodation).OrderBy(i => i.ServiceDate).ToList();
        Assert.Equal(new[] { CheckIn.AddDays(1), CheckIn.AddDays(2) }, accommodation.Select(i => i.ServiceDate));
        Assert.All(accommodation, i => Assert.Equal(2700m, i.UnitPrice));
        Assert.Same(secondNight, accommodation[0]); // the line of the remaining night is kept
        Assert.Contains(breakfast, invoice.Items);
        Assert.Equal(5400m + 350m, invoice.GetTotal());
    }

    [Fact]
    public void ShorterStayCanMakeInvoiceOverpaid()
    {
        var invoice = NewInvoice();
        Pay(invoice, 5000m);

        invoice.SyncAccommodation(Nights(count: 1), Now); // early departure, UC-13 A1

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(-2500m, invoice.GetBalance());
    }

    [Fact]
    public void CloseKeepsPaymentStateAndBlocksNewLines()
    {
        var invoice = NewInvoice();
        Pay(invoice, 1000m);

        invoice.Close(Now);

        Assert.True(invoice.IsClosed);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Throws<DomainException>(() => invoice.AddItem(BreakfastId, "Сніданок", CheckIn, 1, 350m, ReceptionistId, Now));
        Assert.Throws<DomainException>(() => invoice.SyncAccommodation(Nights(count: 3), Now));
        Assert.Throws<DomainException>(() => invoice.Close(Now));

        // A debt may be paid after check-out (UC-13 A2).
        Pay(invoice, 4000m);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void AnyStateCanBeVoidedWithReason(int paid)
    {
        var invoice = NewInvoice();
        if (paid > 0)
        {
            Pay(invoice, paid);
        }

        invoice.Void("Помилково відкритий рахунок", AccountantId, Now);

        Assert.Equal(InvoiceStatus.Voided, invoice.Status);
        Assert.Equal(Now, invoice.VoidedAtUtc);
        Assert.Equal(AccountantId, invoice.VoidedByUserId);
        Assert.Equal("Помилково відкритий рахунок", invoice.VoidReason);
    }

    [Fact]
    public void ClosedInvoiceCanBeVoided()
    {
        var invoice = NewInvoice();
        invoice.Close(Now);

        invoice.Void("Коригування", AccountantId, Now);

        Assert.Equal(InvoiceStatus.Voided, invoice.Status);
    }

    [Fact]
    public void VoidNeedsReason()
    {
        var invoice = NewInvoice();

        Assert.Throws<DomainException>(() => invoice.Void("  ", AccountantId, Now));
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
    }

    public static TheoryData<string> VoidedInvoiceActions() => new()
    {
        "AddItem", "RemoveItem", "SyncAccommodation", "AddPayment", "VoidPayment", "Close", "Void",
    };

    [Theory]
    [MemberData(nameof(VoidedInvoiceActions))]
    public void VoidedIsFinal(string action)
    {
        var invoice = NewInvoice();
        var breakfast = invoice.AddItem(BreakfastId, "Сніданок", CheckIn, 1, 350m, ReceptionistId, Now);
        var payment = Pay(invoice, 1000m);
        invoice.Void("Помилка", AccountantId, Now);

        Action act = action switch
        {
            "AddItem" => () => invoice.AddItem(BreakfastId, "Сніданок", CheckIn, 1, 350m, ReceptionistId, Now),
            "RemoveItem" => () => invoice.RemoveItem(breakfast.Id),
            "SyncAccommodation" => () => invoice.SyncAccommodation(Nights(count: 3), Now),
            "AddPayment" => () => Pay(invoice, 100m),
            "VoidPayment" => () => invoice.VoidPayment(payment.Id, "Помилка", AccountantId, Now),
            "Close" => () => invoice.Close(Now),
            "Void" => () => invoice.Void("Ще раз", AccountantId, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        var error = Assert.Throws<InvalidTransitionException>(act);
        Assert.Contains("«анульований»", error.Message);
        Assert.Equal(InvoiceStatus.Voided, invoice.Status);
        invoice.RecalculateStatus();
        Assert.Equal(InvoiceStatus.Voided, invoice.Status);
    }
}
