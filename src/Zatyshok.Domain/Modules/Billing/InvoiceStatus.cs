namespace Zatyshok.Domain.Modules.Billing;

/// <summary>
/// Payment state of an invoice (table 6.1, table 12.28). Open, PartiallyPaid and Paid are
/// derived from the sums (<see cref="Invoice.RecalculateStatus"/>); Voided is final.
/// </summary>
public enum InvoiceStatus
{
    Open,
    PartiallyPaid,
    Paid,
    Voided,
}
