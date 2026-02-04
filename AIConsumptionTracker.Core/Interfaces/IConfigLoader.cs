using AIConsumptionTracker.Core.Models;

namespace AIConsumptionTracker.Core.Interfaces;

public interface IConfigLoader
{
    Task<List<ProviderConfig>> LoadConfigAsync();
    Task SaveConfigAsync(List<ProviderConfig> configs);
    Task<AppPreferences> LoadPreferencesAsync();
    Task SavePreferencesAsync(AppPreferences preferences);
}

