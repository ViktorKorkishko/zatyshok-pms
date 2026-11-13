namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>
/// The one rule for a city in a search, used by the hotel list and by the availability search (FR-10): a
/// person types the city, so "одеса" and " Одеса " find "Одеса". The comparison is made in C#, not in SQL:
/// lower() in PostgreSQL depends on the collation of the database (Collations.Ukrainian), while the network
/// has at most 20 hotels (NFR-15), so reading the cities of all of them costs nothing.
/// </summary>
internal static class Cities
{
    public static bool Same(string city, string wanted)
        => string.Equals(city.Trim(), wanted.Trim(), StringComparison.OrdinalIgnoreCase);
}
