// <copyright file="MonitorHealthSnapshot.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Core.Models;

public sealed class MonitorHealthSnapshot
{
    public string Status { get; set; } = "unknown";

    public string ServiceHealth { get; set; } = "unknown";

    public DateTime? Timestamp { get; set; }

    public int Port { get; set; }

    public int ProcessId { get; set; }

    public string? AgentVersion { get; set; }

    // Legacy health payload field kept for backward compatibility with older monitor versions.
    public string? Version { get; set; }

    public string? ApiContractVersion { get; set; }

    public MonitorRefreshHealthSnapshot RefreshHealth { get; set; } = new();
}

public sealed class MonitorRefreshHealthSnapshot
{
    public string Status { get; set; } = "unknown";

    public DateTime? LastRefreshAttemptUtc { get; set; }

    public DateTime? LastRefreshCompletedUtc { get; set; }

    public DateTime? LastSuccessfulRefreshUtc { get; set; }

    public string? LastError { get; set; }

    public int ProvidersInBackoff { get; set; }

    public IReadOnlyList<string> FailingProviders { get; set; } = Array.Empty<string>();
}
