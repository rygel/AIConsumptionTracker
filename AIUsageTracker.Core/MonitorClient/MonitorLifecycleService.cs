// <copyright file="MonitorLifecycleService.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Interfaces;

namespace AIUsageTracker.Core.MonitorClient;

public sealed class MonitorLifecycleService : IMonitorLifecycleService
{
    public Task<bool> StartAgentAsync()
    {
        return MonitorLauncher.StartAgentAsync();
    }

    public Task<bool> StopAgentAsync()
    {
        return MonitorLauncher.StopAgentAsync();
    }

    public Task<bool> EnsureAgentRunningAsync()
    {
        return MonitorLauncher.EnsureAgentRunningAsync();
    }

    public Task<bool> WaitForAgentAsync(CancellationToken cancellationToken = default)
    {
        return MonitorLauncher.WaitForAgentAsync(cancellationToken);
    }

    public Task<int> GetAgentPortAsync()
    {
        return MonitorLauncher.GetAgentPortAsync();
    }

    public Task<bool> IsAgentRunningAsync()
    {
        return MonitorLauncher.IsAgentRunningAsync();
    }

    public Task<(bool IsRunning, int Port)> IsAgentRunningWithPortAsync()
    {
        return MonitorLauncher.IsAgentRunningWithPortAsync();
    }

    public Task<MonitorLauncher.MonitorStatusInfo> GetAgentStatusInfoAsync()
    {
        return MonitorLauncher.GetAgentStatusInfoAsync();
    }

    public async Task<MonitorMetadataStatus> GetMonitorMetadataSnapshotAsync()
    {
        var snapshot = await MonitorLauncher.GetMonitorMetadataSnapshotAsync().ConfigureAwait(false);
        return new MonitorMetadataStatus
        {
            IsUsable = snapshot.IsUsable,
            Info = snapshot.Info,
        };
    }
}
