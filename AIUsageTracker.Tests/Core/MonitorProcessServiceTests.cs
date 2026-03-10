// <copyright file="MonitorProcessServiceTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AIUsageTracker.Tests.Core;

public sealed class MonitorProcessServiceTests
{
    [Fact]
    public async Task GetAgentStatusDetailedAsync_ReturnsMissing_WhenMonitorInfoIsAbsentAsync()
    {
        var launcher = new Mock<IMonitorLauncherClient>();
        launcher
            .Setup(client => client.GetAgentStatusInfoAsync())
            .ReturnsAsync(new MonitorAgentStatus
            {
                IsRunning = false,
                Port = 5000,
                HasMetadata = false,
                Message = "Monitor info file not found. Start Monitor to initialize it.",
                Error = "agent-info-missing",
            });

        var service = CreateService(launcherClient: launcher.Object);
        var result = await service.GetAgentStatusDetailedAsync();

        Assert.False(result.IsRunning);
        Assert.Equal(5000, result.Port);
        Assert.Equal("agent-info-missing", result.Error);
        Assert.Null(result.ServiceHealth);
    }

    [Fact]
    public async Task StartAgentDetailedAsync_ReturnsAlreadyRunning_WhenMonitorIsHealthyAsync()
    {
        var launcher = new Mock<IMonitorLauncherClient>();
        launcher
            .Setup(client => client.GetAgentStatusInfoAsync())
            .ReturnsAsync(new MonitorAgentStatus
            {
                IsRunning = true,
                Port = 6222,
                HasMetadata = true,
                Message = "Healthy on port 6222.",
            });

        var service = CreateService(launcherClient: launcher.Object);
        var result = await service.StartAgentDetailedAsync();

        Assert.True(result.Success);
        Assert.Equal("Monitor already running on port 6222.", result.Message);
        Assert.Null(result.Error);
        launcher.Verify(client => client.EnsureAgentRunningAsync(), Times.Never);
    }

    [Fact]
    public async Task StartAgentDetailedAsync_ReturnsStartupFailureDetails_WhenStartupFailsAsync()
    {
        var launcher = new Mock<IMonitorLauncherClient>();
        launcher
            .SetupSequence(client => client.GetAgentStatusInfoAsync())
            .ReturnsAsync(new MonitorAgentStatus
            {
                IsRunning = false,
                Port = 5000,
                HasMetadata = true,
                Message = "Monitor is starting.",
                Error = "monitor-starting",
            })
            .ReturnsAsync(new MonitorAgentStatus
            {
                IsRunning = false,
                Port = 5000,
                HasMetadata = true,
                Message = "Startup status: failed: port bind failed",
                Error = "monitor-startup-failed",
            });
        launcher
            .Setup(client => client.EnsureAgentRunningAsync())
            .ReturnsAsync(false);

        var service = CreateService(launcherClient: launcher.Object);
        var result = await service.StartAgentDetailedAsync();

        Assert.False(result.Success);
        Assert.Equal("monitor-startup-failed", result.Error);
        Assert.Equal("failed", result.StartupState);
        Assert.Equal("port bind failed", result.StartupFailureReason);
        Assert.Equal("Monitor startup failed: port bind failed", result.Message);
    }

    [Fact]
    public async Task GetAgentStatusDetailedAsync_ReturnsDegradedHealthSummary_WhenMonitorIsRunningAsync()
    {
        var launcher = new Mock<IMonitorLauncherClient>();
        launcher
            .Setup(client => client.GetAgentStatusInfoAsync())
            .ReturnsAsync(new MonitorAgentStatus
            {
                IsRunning = true,
                Port = 6333,
                HasMetadata = true,
                Message = "Healthy on port 6333.",
            });

        var monitorService = new Mock<IMonitorService>();
        monitorService
            .Setup(service => service.RefreshAgentInfoAsync())
            .Returns(Task.CompletedTask);
        monitorService
            .Setup(service => service.GetHealthSnapshotAsync())
            .ReturnsAsync(new MonitorHealthSnapshot
            {
                Status = "healthy",
                ServiceHealth = "degraded",
                Port = 6333,
                RefreshHealth = new MonitorRefreshHealthSnapshot
                {
                    LastError = "ProviderManager not ready",
                    ProvidersInBackoff = 2,
                    FailingProviders = ["openai", "anthropic"],
                },
            });

        var service = CreateService(monitorService.Object, launcher.Object);
        var result = await service.GetAgentStatusDetailedAsync();

        Assert.True(result.IsRunning);
        Assert.Equal(6333, result.Port);
        Assert.Equal("degraded", result.ServiceHealth);
        Assert.Equal("ProviderManager not ready", result.LastRefreshError);
        Assert.Equal(2, result.ProvidersInBackoff);
        Assert.Equal(["openai", "anthropic"], result.FailingProviders);
        Assert.Contains("degraded", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StopAgentDetailedAsync_ReturnsAlreadyStopped_WhenMonitorInfoIsAbsentAsync()
    {
        var launcher = new Mock<IMonitorLauncherClient>();
        launcher
            .Setup(client => client.GetAgentStatusInfoAsync())
            .ReturnsAsync(new MonitorAgentStatus
            {
                IsRunning = false,
                Port = 5000,
                HasMetadata = false,
                Message = "Monitor info file not found. Start Monitor to initialize it.",
                Error = "agent-info-missing",
            });

        var service = CreateService(launcherClient: launcher.Object);
        var result = await service.StopAgentDetailedAsync();

        Assert.True(result.Success);
        Assert.Equal("Monitor already stopped (info file missing).", result.Message);
        launcher.Verify(client => client.StopAgentAsync(), Times.Never);
    }

    private static MonitorProcessService CreateService(IMonitorService? monitorService = null, IMonitorLauncherClient? launcherClient = null)
    {
        var monitorServiceMock = monitorService ?? new Mock<IMonitorService>().Object;
        var launcherClientMock = launcherClient ?? new Mock<IMonitorLauncherClient>().Object;
        return new MonitorProcessService(
            NullLogger<MonitorProcessService>.Instance,
            monitorServiceMock,
            launcherClientMock);
    }
}
