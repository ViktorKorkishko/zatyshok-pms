namespace Zatyshok.Infrastructure.Persistence;

/// <summary>Collations used by column configurations (12.1.3).</summary>
public static class Collations
{
    /// <summary>
    /// Ukrainian ICU collation for names of people and organizations: alphabetical order
    /// (Г, Ґ, Д, Е, Є, …) and correct lower() for Cyrillic. The database default (e.g. C.UTF-8)
    /// sorts by character codes and leaves Cyrillic letters unchanged in lower().
    /// </summary>
    public const string Ukrainian = "uk-UA-x-icu";
}
