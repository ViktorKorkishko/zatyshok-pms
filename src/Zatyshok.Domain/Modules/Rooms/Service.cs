namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>
/// Extra service from the network-wide catalog: breakfast, parking, transfer (FR-32, table 12.11).
/// Invoice lines copy the name and price, so later catalog changes do not alter issued invoices.
/// </summary>
public class Service
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Catalog price in UAH (FR-05).</summary>
    public decimal Price { get; set; }

    /// <summary>Services already used in invoices are deactivated instead of deleted (UC-11).</summary>
    public bool IsActive { get; set; } = true;
}
