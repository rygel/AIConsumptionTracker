using AIUsageTracker.Monitor.Services;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AIUsageTracker.Monitor.Tests;

public class ProviderRefreshServiceTests
{
    private readonly Mock<ILogger<ProviderRefreshService>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<IUsageDatabase> _mockDatabase;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IConfigService> _mockConfigService;
    private readonly ProviderRefreshService _service;

    public ProviderRefreshServiceTests()
    {
        _mockLogger = new Mock<ILogger<ProviderRefreshService>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockDatabase = new Mock<IUsageDatabase>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockConfigService = new Mock<IConfigService>();

        _service = new ProviderRefreshService(
            _mockLogger.Object,
            _mockLoggerFactory.Object,
            _mockDatabase.Object,
            _mockNotificationService.Object,
            _mockHttpClientFactory.Object,
            _mockConfigService.Object);
    }

    [Fact]
    public void CheckUsageAlertsAsync_UsageAboveThreshold_TriggersNotification()
    {
        // Arrange
        var prefs = new AppPreferences { EnableNotifications = true, NotificationThreshold = 90.0 };
        var configs = new List<ProviderConfig> 
        { 
            new ProviderConfig { ProviderId = "test", EnableNotifications = true } 
        };
        var usages = new List<ProviderUsage>
        {
            new ProviderUsage 
            { 
                ProviderId = "test", 
                ProviderName = "Test Provider", 
                RequestsPercentage = 95.0,
                IsAvailable = true
            }
        };

        // Act
        _service.CheckUsageAlerts(usages, prefs, configs);

        // Assert
        _mockNotificationService.Verify(n => n.ShowNotification(
            "Test Provider", 
            It.Is<string>(s => s.Contains("95.0")), 
            "openDashboard", 
            "test"), Times.Once);
    }

    [Fact]
    public void CheckUsageAlertsAsync_QuotaRemainingLow_TriggersNotificationFromUsedPercent()
    {
        // Arrange
        var prefs = new AppPreferences { EnableNotifications = true, NotificationThreshold = 90.0 };
        var configs = new List<ProviderConfig>
        {
            new ProviderConfig { ProviderId = "test", EnableNotifications = true }
        };
        var usages = new List<ProviderUsage>
        {
            new ProviderUsage
            {
                ProviderId = "test",
                ProviderName = "Test Provider",
                RequestsPercentage = 95.0, // Used percent for notification check
                IsQuotaBased = true,
                PlanType = PlanType.Coding,
                IsAvailable = true
            }
        };

        // Act
        _service.CheckUsageAlerts(usages, prefs, configs);

        // Assert
        _mockNotificationService.Verify(n => n.ShowNotification(
            "Test Provider", 
            It.Is<string>(s => s.Contains("95.0")), 
            "openDashboard", 
            "test"), Times.Once);
    }

    [Fact]
    public void CheckUsageAlertsAsync_QuotaRemainingHigh_DoesNotTriggerNotification()
    {
        // Arrange
        var prefs = new AppPreferences { EnableNotifications = true, NotificationThreshold = 90.0 };
        var configs = new List<ProviderConfig>
        {
            new ProviderConfig { ProviderId = "test", EnableNotifications = true }
        };
        var usages = new List<ProviderUsage>
        {
            new ProviderUsage
            {
                ProviderId = "test",
                ProviderName = "Test Provider",
                RequestsPercentage = 70.0, // 70% used
                IsQuotaBased = true,
                PlanType = PlanType.Coding,
                IsAvailable = true
            }
        };

        // Act
        _service.CheckUsageAlerts(usages, prefs, configs);

        // Assert
        _mockNotificationService.Verify(n => n.ShowNotification(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CheckUsageAlertsAsync_NotificationsDisabledGlobally_DoesNotTrigger()
    {
        // Arrange
        var prefs = new AppPreferences { EnableNotifications = false, NotificationThreshold = 90.0 };
        var configs = new List<ProviderConfig> 
        { 
            new ProviderConfig { ProviderId = "test", EnableNotifications = true } 
        };
        var usages = new List<ProviderUsage>
        {
            new ProviderUsage 
            { 
                ProviderId = "test", 
                ProviderName = "Test Provider", 
                RequestsPercentage = 95.0,
                IsAvailable = true
            }
        };

        // Act
        _service.CheckUsageAlerts(usages, prefs, configs);

        // Assert
        _mockNotificationService.Verify(n => n.ShowNotification(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CheckUsageAlertsAsync_ProviderNotificationsDisabled_DoesNotTrigger()
    {
        // Arrange
        var prefs = new AppPreferences { EnableNotifications = true, NotificationThreshold = 90.0 };
        var configs = new List<ProviderConfig> 
        { 
            new ProviderConfig { ProviderId = "test", EnableNotifications = false } 
        };
        var usages = new List<ProviderUsage>
        {
            new ProviderUsage 
            { 
                ProviderId = "test", 
                ProviderName = "Test Provider", 
                RequestsPercentage = 95.0,
                IsAvailable = true
            }
        };

        // Act
        _service.CheckUsageAlerts(usages, prefs, configs);

        // Assert
        _mockNotificationService.Verify(n => n.ShowNotification(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetRefreshTelemetrySnapshot_InitialState_IsZeroed()
    {
        var telemetry = _service.GetRefreshTelemetrySnapshot();

        Assert.Equal(0, telemetry.RefreshCount);
        Assert.Equal(0, telemetry.RefreshSuccessCount);
        Assert.Equal(0, telemetry.RefreshFailureCount);
        Assert.Equal(0, telemetry.ErrorRatePercent);
        Assert.Equal(0, telemetry.AverageLatencyMs);
        Assert.Null(telemetry.LastError);
    }

    [Fact]
    public async Task TriggerRefreshAsync_WhenProviderManagerMissing_RecordsFailureTelemetry()
    {
        // Manager is not initialized in CTOR, so first refresh will fail if not set
        await _service.TriggerRefreshAsync();
        var telemetry = _service.GetRefreshTelemetrySnapshot();

        Assert.Equal(1, telemetry.RefreshCount);
        Assert.Equal(0, telemetry.RefreshSuccessCount);
        Assert.Equal(1, telemetry.RefreshFailureCount);
        Assert.True(telemetry.ErrorRatePercent > 0);
        Assert.NotNull(telemetry.LastError);
    }
}
