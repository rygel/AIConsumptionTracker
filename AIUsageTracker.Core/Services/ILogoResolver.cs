namespace AIUsageTracker.Core.Services;

/// <summary>
/// Service for resolving provider logos and fallback icon data.
/// Centralizes logo resolution logic to avoid duplication across UI components.
/// </summary>
public interface ILogoResolver
{
    /// <summary>
    /// Gets the logo filename for a provider (without extension).
    /// Returns null if no mapping exists.
    /// </summary>
    string? GetLogoFilename(string providerId);

    /// <summary>
    /// Gets the fallback icon color (as a brush name) and initials for a provider.
    /// Color names map to WPF Brushes: "RoyalBlue", "DarkCyan", "IndianRed", "MediumPurple", "Orange", "DeepSkyBlue", "Teal", "SlateBlue", "Gray"
    /// </summary>
    (string BrushName, string Initials) GetFallbackIconData(string providerId);

    /// <summary>
    /// Gets the full path to the logo file for a provider.
    /// Returns null if logo doesn't exist.
    /// </summary>
    string? GetLogoPath(string providerId);
}
