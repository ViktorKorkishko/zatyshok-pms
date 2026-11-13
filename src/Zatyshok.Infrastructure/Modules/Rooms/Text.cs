namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Normalization of text fields before they are saved.</summary>
internal static class Text
{
    /// <summary>An optional field: trimmed, or null when it is empty or only spaces.</summary>
    public static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
