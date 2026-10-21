using Zatyshok.Domain.Modules.Identity.Contracts;

namespace Zatyshok.Api.Common.Authorization;

/// <summary>
/// Roles that will have access to an endpoint by the column "Ролі" of table 6.2 (FR-02).
/// <b>Not enforced yet:</b> sign-in and authorization policies come in T4, which replaces these marks
/// with checks. Until then the marks document the plan: Swagger shows them in the description of each
/// endpoint, and an architecture test requires one on every endpoint, so none is forgotten in T4.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowedRolesAttribute(params string[] roles) : Attribute
{
    /// <summary>Role codes (<see cref="Authorization.Roles"/>), or <see cref="Authorization.Roles.Anonymous"/> alone for a public endpoint.</summary>
    public IReadOnlyList<string> Codes { get; } = roles;
}

/// <summary>
/// Role codes for <see cref="AllowedRolesAttribute"/>. The values are the codes of identity.Roles
/// (<see cref="RoleCodes"/>, a contract of Identity); controllers use this class, because a controller
/// of one module may not refer to another module, Identity included (9.4.1, rules 2 and 4).
/// </summary>
public static class Roles
{
    /// <summary>Anyone, without signing in: the guest portal before login (UC-04).</summary>
    public const string Anonymous = "Anonymous";

    /// <summary>Гість.</summary>
    public const string Guest = RoleCodes.Guest;

    /// <summary>Оператор рецепції.</summary>
    public const string Receptionist = RoleCodes.Receptionist;

    /// <summary>Оператор відділу бронювання.</summary>
    public const string ReservationAgent = RoleCodes.ReservationAgent;

    /// <summary>Покоївка.</summary>
    public const string Housekeeper = RoleCodes.Housekeeper;

    /// <summary>Старша покоївка.</summary>
    public const string HeadHousekeeper = RoleCodes.HeadHousekeeper;

    /// <summary>Менеджер готелю.</summary>
    public const string HotelManager = RoleCodes.HotelManager;

    /// <summary>Бухгалтер.</summary>
    public const string Accountant = RoleCodes.Accountant;

    /// <summary>Керівництво мережі.</summary>
    public const string Executive = RoleCodes.Executive;

    /// <summary>Адміністратор системи.</summary>
    public const string Administrator = RoleCodes.Administrator;

    /// <summary>Ukrainian names for Swagger, as in the migration that seeds identity.Roles.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [Anonymous] = "усі, без входу",
        [Guest] = "гість",
        [Receptionist] = "оператор рецепції",
        [ReservationAgent] = "оператор відділу бронювання",
        [Housekeeper] = "покоївка",
        [HeadHousekeeper] = "старша покоївка",
        [HotelManager] = "менеджер готелю",
        [Accountant] = "бухгалтер",
        [Executive] = "керівництво мережі",
        [Administrator] = "адміністратор системи",
    };
}
