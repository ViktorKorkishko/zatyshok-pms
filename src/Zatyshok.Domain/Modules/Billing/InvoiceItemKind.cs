namespace Zatyshok.Domain.Modules.Billing;

/// <summary>Invoice line kind (FR-31, FR-32).</summary>
public enum InvoiceItemKind
{
    /// <summary>One night of the stay; one line per night (12.3.6).</summary>
    Accommodation,

    /// <summary>Extra service from the catalog.</summary>
    Service,
}
