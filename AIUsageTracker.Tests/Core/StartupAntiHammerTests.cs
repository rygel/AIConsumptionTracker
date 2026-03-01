using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Monitor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIUsageTracker.Tests.Core;

public class StartupAntiHammerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDatabaseHasData_DoesNotTriggerFullRefresh()
    {
        var mockLogger = new Mock<ILogger<ProviderRefreshService>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockDb = new Mock<IUsageDatabase>();
        var mockNotificationService = new Mock<INotificationService>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockConfigService = new Mock<ConfigService>(Mock.Of<ILogger<ConfigService>>(), Mock.Of<IConfigLoader>());

        mockDb.Setup(db => db.IsHistoryEmptyAsync())
            .ReturnsAsync(false);

        mockConfigService.Setup(cs => cs.GetConfigsAsync())
            .ReturnsAsync(new List<ProviderConfig>());

        var service = new ProviderRefreshService(
            mockLogger.Object,
            mockLoggerFactory.Object,
            mockDb.Object,
            mockNotificationService.Object,
            mockHttpClientFactory.Object,
            mockConfigService.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        await service.ExecuteAsync(cts.Token);

        mockDb.Verify(db => db.IsHistoryEmptyAsync(), Times.Once);
        mockConfigService.Verify(cs => cs.GetConfigsAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDatabaseIsEmpty_TriggersFullRefresh()
    {
        var mockLogger = new Mock<ILogger<ProviderRefreshService>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockDb = new Mock<IUsageDatabase>();
        var mockNotificationService = new Mock<INotificationService>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockConfigService = new Mock<ConfigService>(Mock.Of<ILogger<ConfigService>>(), Mock.Of<IConfigLoader>());

        mockDb.Setup(db => db.IsHistoryEmptyAsync())
            .ReturnsAsync(true);

        mockConfigService.SetupSequence(cs => cs.GetConfigsAsync())
            .ReturnsAsync(new List<ProviderConfig>())
            .ReturnsAsync(new List<ProviderConfig>());

        var service = new ProviderRefreshService(
            mockLogger.Object,
            mockLoggerFactory.Object,
            mockDb.Object,
            mockNotificationService.Object,
            mockHttpClientFactory.Object,
            mockConfigService.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        await service.ExecuteAsync(cts.Token);

        mockDb.Verify(db => db.IsHistoryEmptyAsync(), Times.Once);
        mockConfigService.Verify(cs => cs.GetConfigsAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void ProviderRefreshService_HasExecuteAsyncMethod()
    {
        var type = typeof(ProviderRefreshService);
        var executeMethod = type.GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(executeMethod);
        Assert.True(executeMethod.ReturnType == typeof(Task), 
            "ExecuteAsync should return Task");
    }

    [Fact]
    public void TriggerRefreshAsync_AcceptsIncludeProviderIdsParameter()
    {
        var type = typeof(ProviderRefreshService);
        var method = type.GetMethod("TriggerRefreshAsync");
        
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        
        var includeProviderIdsParam = parameters.FirstOrDefault(p => p.Name == "includeProviderIds");
        Assert.NotNull(includeProviderIdsParam);
        Assert.True(includeProviderIdsParam.ParameterType == typeof(IReadOnlyCollection<string>),
            "includeProviderIds should be IReadOnlyCollection<string>");
    }
}
