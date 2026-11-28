namespace Zatyshok.Domain.Common;

/// <summary>Money rules shared by Bookings and Billing: amounts in UAH, exact to the kopiyka (NFR-20).</summary>
public static class Money
{
    /// <summary>
    /// Rejects an amount with more than two decimal places. The money columns are numeric(12,2),
    /// so PostgreSQL would round such an amount on save, and the saved sums would no longer match
    /// what the domain calculated from them: for example, an invoice saved as PartiallyPaid although
    /// after rounding P = T, or a payment of 0.004 that passes the domain but fails CK_Payments_Amount.
    /// </summary>
    /// <param name="what">What the amount is, as the start of the message, e.g. "Сума платежу".</param>
    public static void EnsureTwoDecimals(decimal amount, string what)
    {
        // decimal.Round keeps the value of 2500.10 and 2500.100 alike, so trailing zeros pass.
        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException($"{what} має бути з точністю до копійки (не більше двох знаків після коми).");
        }
    }
}
