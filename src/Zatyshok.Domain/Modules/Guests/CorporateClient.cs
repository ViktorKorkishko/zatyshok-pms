namespace Zatyshok.Domain.Modules.Guests;

/// <summary>Organization that pays for its employees' stays (FR-26, table 12.16).</summary>
public class CorporateClient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>EDRPOU code: 8 digits (CK_CorporateClients_Edrpou; the length is an assumption, 12.3.4).</summary>
    public required string Edrpou { get; set; }

    public string? ContractNumber { get; set; }

    public DateOnly? ContractStartDate { get; set; }

    /// <summary>Not earlier than <see cref="ContractStartDate"/> (CK_CorporateClients_Contract).</summary>
    public DateOnly? ContractEndDate { get; set; }

    /// <summary>Deferral of the cashless payment, days (FR-26).</summary>
    public int PaymentDeferralDays { get; set; }

    public string? ContactPerson { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    /// <summary>Clients referenced by bookings or invoices are deactivated instead of deleted.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
