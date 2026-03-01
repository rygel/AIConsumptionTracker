using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIUsageTracker.Tests.Infrastructure;

public class TokenDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverTokensAsync_IncludesCodexAsWellKnownProvider()
    {
        // Arrange
        var discovery = new TokenDiscoveryService(NullLogger<TokenDiscoveryService>.Instance);

        // Act
        var configs = await discovery.DiscoverTokensAsync();

        // Assert
        var codex = configs.FirstOrDefault(c => c.ProviderId.Equals("codex", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(codex);
        Assert.Equal(PlanType.Coding, codex!.PlanType);
        Assert.Equal("quota-based", codex.Type);
    }
}
