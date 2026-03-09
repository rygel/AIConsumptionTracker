// <copyright file="MonitorStartupPathTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Text.Json;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AIUsageTracker.Tests.Core;

[Collection("MonitorStartupPath")]
public sealed class MonitorStartupPathTests : IDisposable
{
    private readonly string _tempDirectory;

    public MonitorStartupPathTests()
    {
        this._tempDirectory = Path.Combine(Path.GetTempPath(), "monitor-startup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._tempDirectory);
    }

    [Fact]
    public async Task GetAndValidateMonitorInfoAsync_ReturnsMonitorInfo_WhenMetadataIsHealthyAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5123,
            ProcessId = 4242,
            Errors = new List<string> { "warning" },
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: port => Task.FromResult(port == 5123),
            processRunningAsync: processId => Task.FromResult(processId == 4242));

        var result = await MonitorLauncher.GetAndValidateMonitorInfoAsync();

        Assert.NotNull(result);
        Assert.Equal(5123, result!.Port);
        Assert.Equal(4242, result.ProcessId);
        Assert.True(File.Exists(infoPath));
    }

    [Fact]
    public async Task GetAndValidateMonitorInfoAsync_InvalidatesMonitorInfo_WhenMetadataIsStaleAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5124,
            ProcessId = 9999,
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: _ => Task.FromResult(false),
            processRunningAsync: _ => Task.FromResult(false));

        var result = await MonitorLauncher.GetAndValidateMonitorInfoAsync();

        Assert.Null(result);
        Assert.False(File.Exists(infoPath));
        Assert.Single(Directory.GetFiles(this._tempDirectory, "monitor.json.stale.*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task GetAndValidateMonitorInfoAsync_InvalidatesMonitorInfo_WhenMetadataIsMalformedAsync()
    {
        var infoPath = await this.CreateMonitorInfoContentAsync("{ not valid json");

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath });

        var result = await MonitorLauncher.GetAndValidateMonitorInfoAsync();

        Assert.Null(result);
        Assert.False(File.Exists(infoPath));
        Assert.Single(Directory.GetFiles(this._tempDirectory, "monitor.json.stale.*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void GetReadCandidatePaths_ReturnsCanonicalPathOnly()
    {
        var appDataRoot = Path.Combine(this._tempDirectory, "appdata");
        var candidates = MonitorInfoPathCatalog.GetReadCandidatePaths(appDataRoot, this._tempDirectory);

        Assert.Collection(candidates, path => Assert.Equal(Path.Combine(appDataRoot, "AIUsageTracker", "monitor.json"), path));
    }

    [Fact]
    public async Task RefreshAgentInfoAsync_UsesValidMonitorInfoPortAndErrorsAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5333,
            ProcessId = 1111,
            Errors = new List<string> { "Startup status: running" },
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: port => Task.FromResult(port == 5333),
            processRunningAsync: processId => Task.FromResult(processId == 1111));

        var service = this.CreateMonitorService();

        await service.RefreshAgentInfoAsync();

        Assert.Equal("http://localhost:5333", service.AgentUrl);
        Assert.Single(service.LastAgentErrors);
        Assert.Contains("Startup status: running", service.LastAgentErrors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAgentInfoAsync_FallsBackToDefaultPortAndClearsErrors_WhenMetadataIsStaleAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5444,
            ProcessId = 2222,
            Errors = new List<string> { "stale" },
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: _ => Task.FromResult(false),
            processRunningAsync: _ => Task.FromResult(false));

        var service = this.CreateMonitorService();
        service.AgentUrl = "http://localhost:5444";

        await service.RefreshAgentInfoAsync();

        Assert.Equal("http://localhost:5000", service.AgentUrl);
        Assert.Empty(service.LastAgentErrors);
        Assert.Single(Directory.GetFiles(this._tempDirectory, "monitor.json.stale.*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task IsAgentRunningWithPortAsync_ReturnsHealthyMetadataPortAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5666,
            ProcessId = 3333,
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: port => Task.FromResult(port == 5666),
            processRunningAsync: processId => Task.FromResult(processId == 3333));

        var result = await MonitorLauncher.IsAgentRunningWithPortAsync();

        Assert.True(result.IsRunning);
        Assert.Equal(5666, result.Port);
    }

    [Fact]
    public async Task EnsureAgentRunningAsync_ReturnsTrue_WhenMonitorIsAlreadyHealthyAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5777,
            ProcessId = 4444,
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: port => Task.FromResult(port == 5777),
            processRunningAsync: processId => Task.FromResult(processId == 4444));

        var result = await MonitorLauncher.EnsureAgentRunningAsync();

        Assert.True(result);
        Assert.True(File.Exists(infoPath));
    }

    [Fact]
    public async Task WaitForAgentAsync_ReturnsFalse_WhenCancelledAsync()
    {
        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: Array.Empty<string>(),
            healthCheckAsync: _ => Task.FromResult(false),
            processRunningAsync: _ => Task.FromResult(false));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var result = await MonitorLauncher.WaitForAgentAsync(cancellationTokenSource.Token);

        Assert.False(result);
    }

    [Fact]
    public async Task StopAgentAsync_InvalidatesMetadata_WhenKnownProcessStopsAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5888,
            ProcessId = 5555,
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: port => Task.FromResult(port == 5888),
            processRunningAsync: processId => Task.FromResult(processId == 5555),
            stopProcessAsync: processId => Task.FromResult(processId == 5555));

        var result = await MonitorLauncher.StopAgentAsync();

        Assert.True(result);
        Assert.False(File.Exists(infoPath));
        Assert.Single(Directory.GetFiles(this._tempDirectory, "monitor.json.stale.*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task StopAgentAsync_ReturnsFalse_WhenStopFailsAndHealthRemainsUpAsync()
    {
        var infoPath = await this.CreateMonitorInfoAsync(new MonitorInfo
        {
            Port = 5999,
            ProcessId = 6666,
        });

        using var _ = MonitorLauncher.PushTestOverrides(
            monitorInfoCandidatePaths: new[] { infoPath },
            healthCheckAsync: port => Task.FromResult(port == 5999),
            processRunningAsync: processId => Task.FromResult(processId == 6666),
            stopProcessAsync: _ => Task.FromResult(false),
            stopNamedProcessesAsync: () => Task.FromResult(false));

        var result = await MonitorLauncher.StopAgentAsync();

        Assert.False(result);
        Assert.True(File.Exists(infoPath));
        Assert.Empty(Directory.GetFiles(this._tempDirectory, "monitor.json.stale.*", SearchOption.TopDirectoryOnly));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(this._tempDirectory))
        {
            Directory.Delete(this._tempDirectory, recursive: true);
        }
    }

    private MonitorService CreateMonitorService()
    {
        return new MonitorService(new HttpClient(new Mock<HttpMessageHandler>().Object), NullLogger<MonitorService>.Instance);
    }

    private async Task<string> CreateMonitorInfoAsync(MonitorInfo info)
    {
        var json = JsonSerializer.Serialize(info);
        return await this.CreateMonitorInfoContentAsync(json).ConfigureAwait(false);
    }

    private async Task<string> CreateMonitorInfoContentAsync(string content)
    {
        var path = Path.Combine(this._tempDirectory, "monitor.json");
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
        return path;
    }
}
