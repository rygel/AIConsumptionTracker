namespace AIUsageTracker.Core.Interfaces
{
    using AIUsageTracker.Core.Models;

    public interface IWebDatabaseRepository
    {
        Task<IReadOnlyList<ProviderUsage>> GetHistorySamplesAsync(IEnumerable<string> providerIds, int lookbackHours, int maxSamples);

        Task<IReadOnlyList<ProviderUsage>> GetAllHistoryForExportAsync(int limit = 0);
    }
}
