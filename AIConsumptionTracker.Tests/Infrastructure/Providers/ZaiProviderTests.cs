using System.Net;
using System.Text.Json;
using AIConsumptionTracker.Core.Models;
using AIConsumptionTracker.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AIConsumptionTracker.Tests.Infrastructure.Providers;

public class ZaiProviderTests
{
    private readonly Mock<HttpMessageHandler> _msgHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<ZaiProvider>> _logger;
    private readonly ZaiProvider _provider;

    public ZaiProviderTests()
    {
        _msgHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_msgHandler.Object);
        _logger = new Mock<ILogger<ZaiProvider>>();
        _provider = new ZaiProvider(_httpClient, _logger.Object);
    }

    [Fact]
    public async Task GetUsageAsync_InvertedCalculation_ReturnsCorrectUsedPercentage()
    {
        // Arrange
        var config = new ProviderConfig { ProviderId = "zai-coding-plan", ApiKey = "test-key" };
        
        // Scenario: 
        // Total Limit = 100
        // Current Value (Remaining) = 90
        // Expected Used = 10%
        
        // If the current implementation is inverted, it calculates 90/100 * 100 = 90%.
        // The fix should calculate (100-90)/100 * 100 = 10%.

        // Assuming JSON structure matches ZaiProvider.cs
        var responseContent = JsonSerializer.Serialize(new
        {
            data = new
            {
                limits = new[]
                {
                    new
                    {
                        type = "TOKENS_LIMIT",
                        percentage = (double?)null, 
                        currentValue = 0, // Unused
                        usage = 100 // mapped to Total property
                    }
                }
            }
        });

        _msgHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _provider.GetUsageAsync(config);

        // Assert
        var usage = result.Single();
        Assert.Equal("Z.AI Coding Plan", usage.ProviderName); // Or Coding Plan
        
        // Scenario: 0 Used (CurrentValue=0), 100 Total.
        // User wants "Completely Filled" bar.
        // Expected Percentage = 100%. (Remaining)
        Assert.Equal(100, usage.UsagePercentage);
        
        Assert.Contains("remaining", usage.Description);
    }
}
