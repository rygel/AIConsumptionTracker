namespace AIUsageTracker.Monitor.Services
{
    using AIUsageTracker.Core.Models;

    public interface IConfigService
    {
        Task<List<ProviderConfig>> GetConfigsAsync();
    `n    Task SaveConfigAsync(ProviderConfig config);
    `n    Task RemoveConfigAsync(string providerId);
    `n    Task<AppPreferences> GetPreferencesAsync();
    `n    Task SavePreferencesAsync(AppPreferences preferences);
    `n    Task<List<ProviderConfig>> ScanForKeysAsync();
    }
}
