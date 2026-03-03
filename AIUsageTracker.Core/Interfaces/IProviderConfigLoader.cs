using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Core.Interfaces;

/// <summary>
/// Interface for loading and saving provider configuration.
/// </summary>
public interface IProviderConfigLoader
{
    /// <summary>
    /// Loads provider configurations from auth.json and providers.json files.
    /// </summary>
    /// <returns>A list of provider configurations.</returns>
    Task<List<ProviderConfig>> LoadConfigAsync();

    /// <summary>
    /// Saves provider configurations to auth.json and providers.json files.
    /// </summary>
    /// <param name="configs">The list of provider configurations to save.</param>
    Task SaveConfigAsync(List<ProviderConfig> configs);
}
