// <copyright file="AgentScanKeysResponse.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Core.MonitorClient;

internal sealed class AgentScanKeysResponse
{
    [JsonPropertyName("discovered")]
    public int Discovered { get; init; }

    [JsonPropertyName("configs")]
    public IReadOnlyList<ProviderConfig>? Configs { get; init; }
}
