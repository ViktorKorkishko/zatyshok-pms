namespace Zatyshok.Domain.Common;

/// <summary>
/// Hotels the current user may see and change (FR-02, NFR-08): one hotel for hotel-bound roles
/// (receptionist, housekeeping, hotel manager) or the whole network. Application services take it
/// as a required parameter and filter their queries by it explicitly (9.6). Until sign-in comes in
/// T4 the API passes a scope of the whole network.
/// </summary>
public interface IHotelScope
{
    /// <summary>The only hotel of the user, or null for the whole network.</summary>
    Guid? HotelId { get; }
}

/// <summary>Checks of a <see cref="IHotelScope"/> shared by the modules.</summary>
public static class HotelScopeExtensions
{
    public static bool Includes(this IHotelScope scope, Guid hotelId)
        => scope.HotelId is null || scope.HotelId == hotelId;

    /// <summary>
    /// For a hotel the user named explicitly, e.g. in a filter or in the body of a new room: a hotel
    /// outside the scope gives 403 (9.6). A record of such a hotel found by its own id gives 404 instead.
    /// </summary>
    public static void EnsureIncludes(this IHotelScope scope, Guid hotelId)
    {
        if (!scope.Includes(hotelId))
        {
            throw new HotelScopeException();
        }
    }
}

/// <summary>A hotel outside the user's scope was named explicitly (9.6); the API answers 403.</summary>
public class HotelScopeException() : Exception("Цей готель поза вашою областю видимості.");
