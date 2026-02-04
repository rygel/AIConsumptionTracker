using AIConsumptionTracker.Core.Models;

namespace AIConsumptionTracker.Core.Interfaces;

public interface IProviderService
{
    string ProviderId { get; }
    Task<ProviderUsage> GetUsageAsync(ProviderConfig config);
}

