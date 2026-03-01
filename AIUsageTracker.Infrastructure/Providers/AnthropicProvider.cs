using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.Infrastructure.Providers;

public class AnthropicProvider : IProviderService
{
    public static ProviderDefinition StaticDefinition { get; } = new(
        providerId: "anthropic",
        displayName: "Anthropic",
        planType: PlanType.Usage,
        isQuotaBased: false,
        defaultConfigType: "pay-as-you-go");

    public ProviderDefinition Definition => StaticDefinition;
    public string ProviderId => StaticDefinition.ProviderId;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(ILogger<AnthropicProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<ProviderUsage>> GetUsageAsync(ProviderConfig config, Action<ProviderUsage>? progressCallback = null)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            return new[]
            {
                new ProviderUsage
                {
                    ProviderId = ProviderId,
                    ProviderName = "Anthropic",
                    IsAvailable = false,
                    Description = "API Key missing",
                    IsQuotaBased = false,
                    PlanType = PlanType.Usage,
                    AuthSource = config.AuthSource
                }
            };
        }

        return new[]
        {
            new ProviderUsage
            {
                ProviderId = ProviderId,
                ProviderName = "Anthropic",
                IsAvailable = true,
                RequestsPercentage = 0.0,
                IsQuotaBased = false,
                PlanType = PlanType.Usage,
                Description = "Connected (Check Dashboard)",
                UsageUnit = "Status",
                AuthSource = config.AuthSource
            }
        };
    }
}


