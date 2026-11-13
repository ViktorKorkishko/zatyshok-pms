namespace Zatyshok.Domain.Modules.Identity;

/// <summary>
/// One of the nine roles of section 4 of report 1 (table 12.2). The rows are seeded in every
/// environment with fixed ids; authorization policies refer to <see cref="Code"/> (<see cref="RoleCodes"/>).
/// </summary>
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Code { get; set; }

    /// <summary>Display name per table 4.1.</summary>
    public required string Name { get; set; }

    /// <summary>Staff role: signs in to the staff workplace.</summary>
    public bool IsStaff { get; set; }

    /// <summary>The role works within one hotel, so the user must have a hotel (UC-10 A1). Only staff roles can be hotel-bound.</summary>
    public bool IsHotelBound { get; set; }
}
