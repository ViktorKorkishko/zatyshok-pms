namespace Zatyshok.Domain.Modules.Identity.Contracts;

/// <summary>
/// Values of <see cref="Role.Code"/> (table 12.2) for the seed and authorization policies. A contract of
/// Identity, because the shared API code (role marks of the endpoints) uses them too (9.4.1, rule 2).
/// </summary>
public static class RoleCodes
{
    public const string Guest = "Guest";
    public const string Receptionist = "Receptionist";
    public const string ReservationAgent = "ReservationAgent";
    public const string Housekeeper = "Housekeeper";
    public const string HeadHousekeeper = "HeadHousekeeper";
    public const string HotelManager = "HotelManager";
    public const string Accountant = "Accountant";
    public const string Executive = "Executive";
    public const string Administrator = "Administrator";
}
