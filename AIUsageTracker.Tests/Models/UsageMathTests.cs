using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Tests.Models;

public class UsageMathTests
{
    [Fact]
    public void CalculateBurnRateForecast_WithPositiveTrend_ReturnsForecast()
    {
        // Arrange
        var start = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        var history = new List<ProviderUsage>
        {
            CreateSample(start, used: 10, available: 100),
            CreateSample(start.AddHours(12), used: 20, available: 100),
            CreateSample(start.AddHours(24), used: 34, available: 100)
        };

        // Act
        var forecast = UsageMath.CalculateBurnRateForecast(history);

        // Assert
        Assert.True(forecast.IsAvailable);
        Assert.Equal(24, forecast.BurnRatePerDay, 3);
        Assert.Equal(66, forecast.RemainingUnits, 3);
        Assert.Equal(2.75, forecast.DaysUntilExhausted, 3);
        Assert.NotNull(forecast.EstimatedExhaustionUtc);
    }

    [Fact]
    public void CalculateBurnRateForecast_AfterReset_UsesLatestCycleOnly()
    {
        // Arrange
        var start = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        var history = new List<ProviderUsage>
        {
            CreateSample(start, used: 70, available: 100),
            CreateSample(start.AddHours(10), used: 80, available: 100),
            CreateSample(start.AddHours(20), used: 5, available: 100),  // reset
            CreateSample(start.AddHours(30), used: 15, available: 100)
        };

        // Act
        var forecast = UsageMath.CalculateBurnRateForecast(history);

        // Assert
        Assert.True(forecast.IsAvailable);
        Assert.Equal(24, forecast.BurnRatePerDay, 3);
        Assert.Equal(85, forecast.RemainingUnits, 3);
        Assert.Equal(3.542, forecast.DaysUntilExhausted, 3);
    }

    [Fact]
    public void CalculateBurnRateForecast_WithInsufficientHistory_ReturnsUnavailable()
    {
        // Arrange
        var history = new List<ProviderUsage>
        {
            CreateSample(DateTime.UtcNow, used: 10, available: 100)
        };

        // Act
        var forecast = UsageMath.CalculateBurnRateForecast(history);

        // Assert
        Assert.False(forecast.IsAvailable);
        Assert.Equal("Insufficient history", forecast.Reason);
    }

    [Fact]
    public void CalculateBurnRateForecast_WithNoConsumptionTrend_ReturnsUnavailable()
    {
        // Arrange
        var start = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        var history = new List<ProviderUsage>
        {
            CreateSample(start, used: 10, available: 100),
            CreateSample(start.AddHours(12), used: 10, available: 100),
            CreateSample(start.AddHours(24), used: 10, available: 100)
        };

        // Act
        var forecast = UsageMath.CalculateBurnRateForecast(history);

        // Assert
        Assert.False(forecast.IsAvailable);
        Assert.Equal("No consumption trend", forecast.Reason);
    }

    private static ProviderUsage CreateSample(DateTime fetchedAt, double used, double available)
    {
        return new ProviderUsage
        {
            ProviderId = "test-provider",
            RequestsUsed = used,
            RequestsAvailable = available,
            FetchedAt = fetchedAt,
            IsAvailable = true
        };
    }
}
