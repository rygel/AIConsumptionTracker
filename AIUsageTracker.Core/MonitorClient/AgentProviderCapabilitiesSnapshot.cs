// <copyright file="AgentProviderCapabilitiesSnapshot.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Core.MonitorClient;

public sealed class AgentProviderCapabilitiesSnapshot
{
    public string ContractVersion { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public IReadOnlyList<AgentProviderCapabilityDefinition> Providers { get; set; } = Array.Empty<AgentProviderCapabilityDefinition>();
}
