using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.Tests.Infrastructure;

public class ProviderMetadataCatalogCanonicalizationTests
{
    [Fact]
    public void NormalizeCanonicalConfigurations_MergesCodexSparkIntoCodex()
    {
        var configs = new List<ProviderConfig>
        {
            new()
            {
                ProviderId = "codex.spark",
                ApiKey = "spark-token",
                AuthSource = "Spark",
                Description = "spark",
                BaseUrl = "https://example.invalid",
                ShowInTray = true,
                EnableNotifications = true
            }
        };

        ProviderMetadataCatalog.NormalizeCanonicalConfigurations(configs);

        var codex = Assert.Single(configs);
        Assert.Equal("codex", codex.ProviderId);
        Assert.Equal("spark-token", codex.ApiKey);
        Assert.Equal("Spark", codex.AuthSource);
        Assert.Equal("spark", codex.Description);
        Assert.Equal("https://example.invalid", codex.BaseUrl);
        Assert.True(codex.ShowInTray);
        Assert.True(codex.EnableNotifications);
        Assert.Equal(PlanType.Coding, codex.PlanType);
        Assert.Equal("quota-based", codex.Type);
    }
}
