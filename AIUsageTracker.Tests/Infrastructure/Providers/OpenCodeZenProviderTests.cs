using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;
using AIUsageTracker.Tests.Infrastructure;
using Xunit;

namespace AIUsageTracker.Tests.Infrastructure.Providers;

public class OpenCodeZenProviderTests : HttpProviderTestBase<OpenCodeZenProvider>
{
    private readonly OpenCodeZenProvider _provider;

    public OpenCodeZenProviderTests()
    {
        _provider = new OpenCodeZenProvider(Logger.Object);
        Config.ApiKey = "test-key";
    }

    [Fact]
    public async Task GetUsageAsync_CliNotFound_ReturnsUnavailable()
    {
        // Arrange
        var provider = new OpenCodeZenProvider(Logger.Object, "non-existent-cli");

        // Act
        var result = await provider.GetUsageAsync(Config);

        // Assert
        var usage = result.Single();
        Assert.False(usage.IsAvailable);
        Assert.Equal(404, usage.HttpStatus);
        Assert.Contains("CLI not found", usage.Description);
    }
}
