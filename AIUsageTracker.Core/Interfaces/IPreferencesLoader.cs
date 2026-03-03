using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Core.Interfaces;

/// <summary>
/// Interface for loading and saving application preferences.
/// </summary>
public interface IPreferencesLoader
{
    /// <summary>
    /// Loads application preferences from auth.json (app_settings).
    /// </summary>
    /// <returns>The application preferences.</returns>
    Task<AppPreferences> LoadPreferencesAsync();

    /// <summary>
    /// Saves application preferences to auth.json (app_settings).
    /// </summary>
    /// <param name="preferences">The application preferences to save.</param>
    Task SavePreferencesAsync(AppPreferences preferences);
}
