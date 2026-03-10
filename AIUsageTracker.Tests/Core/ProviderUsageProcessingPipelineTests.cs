// <copyright file="ProviderUsageProcessingPipelineTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Monitor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIUsageTracker.Tests.Core;

public sealed class ProviderUsageProcessingPipelineTests
{
    [Fact]
    public void GetSnapshot_BeforeProcess_ReturnsZeroedTelemetry()
    {
        var pipeline = CreatePipeline();

        var snapshot = pipeline.GetSnapshot();

        Assert.Equal(0, snapshot.TotalProcessedEntries);
        Assert.Equal(0, snapshot.TotalAcceptedEntries);
        Assert.Equal(0, snapshot.TotalRejectedEntries);
        Assert.Equal(0, snapshot.InvalidIdentityCount);
        Assert.Equal(0, snapshot.InactiveProviderFilteredCount);
        Assert.Equal(0, snapshot.PlaceholderFilteredCount);
        Assert.Equal(0, snapshot.DetailContractAdjustedCount);
        Assert.Equal(0, snapshot.NormalizedCount);
        Assert.Equal(0, snapshot.PrivacyRedactedCount);
        Assert.Null(snapshot.LastProcessedAtUtc);
        Assert.Equal(0, snapshot.LastRunTotalEntries);
        Assert.Equal(0, snapshot.LastRunAcceptedEntries);
    }

    [Fact]
    public void Process_MultipleRuns_AccumulatesTelemetrySnapshot()
    {
        var pipeline = CreatePipeline();

        _ = pipeline.Process(CreateFirstRunUsages(), ["openai"], isPrivacyMode: true);
        _ = pipeline.Process(CreateSecondRunUsages(), ["openai"], isPrivacyMode: false);

        var snapshot = pipeline.GetSnapshot();

        Assert.Equal(6, snapshot.TotalProcessedEntries);
        Assert.Equal(3, snapshot.TotalAcceptedEntries);
        Assert.Equal(3, snapshot.TotalRejectedEntries);
        Assert.Equal(1, snapshot.InvalidIdentityCount);
        Assert.Equal(1, snapshot.InactiveProviderFilteredCount);
        Assert.Equal(1, snapshot.PlaceholderFilteredCount);
        Assert.Equal(1, snapshot.DetailContractAdjustedCount);
        Assert.True(snapshot.NormalizedCount >= 1);
        Assert.Equal(1, snapshot.PrivacyRedactedCount);
        Assert.NotNull(snapshot.LastProcessedAtUtc);
        Assert.Equal(DateTimeKind.Utc, snapshot.LastProcessedAtUtc!.Value.Kind);
        Assert.Equal(1, snapshot.LastRunTotalEntries);
        Assert.Equal(1, snapshot.LastRunAcceptedEntries);
    }

    private static ProviderUsageProcessingPipeline CreatePipeline()
    {
        return new ProviderUsageProcessingPipeline(NullLogger<ProviderUsageProcessingPipeline>.Instance);
    }

    private static IReadOnlyList<ProviderUsage> CreateFirstRunUsages()
    {
        return
        [
            new ProviderUsage
            {
                ProviderId = " openai ",
                ProviderName = " OpenAI ",
                IsAvailable = true,
                RequestsUsed = double.NaN,
                RequestsAvailable = 100,
                RequestsPercentage = double.PositiveInfinity,
                RawJson = "{ \"key\": \"value\" }",
                AccountName = "user@example.com",
                ConfigKey = "cfg-openai",
                FetchedAt = default,
            },
            new ProviderUsage
            {
                ProviderId = string.Empty,
                ProviderName = "Invalid",
                IsAvailable = true,
            },
            new ProviderUsage
            {
                ProviderId = "anthropic",
                ProviderName = "Anthropic",
                IsAvailable = true,
            },
            new ProviderUsage
            {
                ProviderId = "openai",
                ProviderName = "OpenAI",
                IsAvailable = false,
                Description = "API Key missing",
                RequestsUsed = 0,
                RequestsAvailable = 0,
                RequestsPercentage = 0,
            },
            new ProviderUsage
            {
                ProviderId = "openai",
                ProviderName = "OpenAI",
                IsAvailable = true,
                Details =
                [
                    new ProviderUsageDetail
                    {
                        Name = string.Empty,
                        DetailType = ProviderUsageDetailType.Unknown,
                        WindowKind = WindowKind.None,
                    },
                ],
            },
        ];
    }

    private static IReadOnlyList<ProviderUsage> CreateSecondRunUsages()
    {
        return
        [
            new ProviderUsage
            {
                ProviderId = "openai",
                ProviderName = "OpenAI",
                IsAvailable = true,
                RequestsUsed = 5,
                RequestsAvailable = 100,
                RequestsPercentage = 5,
                FetchedAt = DateTime.UtcNow,
            },
        ];
    }
}
