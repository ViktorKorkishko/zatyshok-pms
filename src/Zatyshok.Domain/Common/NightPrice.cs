namespace Zatyshok.Domain.Common;

/// <summary>
/// Price of one night of a stay; a night is named by the date it starts (12.3.2).
/// Shared by Bookings (booking nights) and Billing (accommodation invoice lines), so that
/// neither module has to reference the other one's entities (9.4.1, rule 2).
/// </summary>
public record NightPrice(DateOnly Night, decimal Price);
