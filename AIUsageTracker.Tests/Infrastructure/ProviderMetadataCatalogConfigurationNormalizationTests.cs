// <copyright file="ProviderMetadataCatalogConfigurationNormalizationTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.Tests.Infrastructure;

public class ProviderMetadataCatalogConfigurationNormalizationTests
{
    private static readonly string TestApiKey1 = Guid.NewGuid().ToString();
    private static readonly string TestApiKey2 = Guid.NewGuid().ToString();
    private static readonly string TestApiKey3 = Guid.NewGuid().ToString();
    private static readonly string TestApiKey4 = Guid.NewGuid().ToString();

    [Fact]
    public void NormalizeProviderConfigurations_KeepsOpenAiSessionTokenOnOpenAiProvider()
    {
        var configs = new List<ProviderConfig>
        {
            new()
            {
                ProviderId = "openai",
                ApiKey = TestApiKey1,
                AuthSource = "OpenCode",
                Description = "session",
            },
        };

        ProviderMetadataCatalog.NormalizeProviderConfigurations(configs);

        var openAi = Assert.Single(configs);
        Assert.Equal("openai", openAi.ProviderId);
        Assert.Equal(TestApiKey1, openAi.ApiKey);
        Assert.Equal("OpenCode", openAi.AuthSource);
        Assert.Equal("session", openAi.Description);
    }

    [Fact]
    public void NormalizeProviderConfigurations_KeepsCodexSparkAsDedicatedProvider()
    {
        var configs = new List<ProviderConfig>
        {
            new()
            {
                ProviderId = "codex.spark",
                ApiKey = TestApiKey2,
                AuthSource = "Spark",
                Description = "spark",
                BaseUrl = "https://example.invalid",
                ShowInTray = true,
                EnableNotifications = true,
            },
        };

        ProviderMetadataCatalog.NormalizeProviderConfigurations(configs);

        var spark = Assert.Single(configs);
        Assert.Equal("codex.spark", spark.ProviderId);
        Assert.Equal(TestApiKey2, spark.ApiKey);
        Assert.Equal("Spark", spark.AuthSource);
        Assert.Equal("spark", spark.Description);
        Assert.Equal("https://example.invalid", spark.BaseUrl);
        Assert.True(spark.ShowInTray);
        Assert.True(spark.EnableNotifications);
    }

    [Fact]
    public void NormalizeProviderConfigurations_KeepsMinimaxCodingPlanAsDedicatedProvider()
    {
        // minimax-coding-plan must NOT be merged into the minimax (China) config —
        // it's a separate visible derived provider with its own API key and endpoint.
        var configs = new List<ProviderConfig>
        {
            new()
            {
                ProviderId = "minimax-coding-plan",
                ApiKey = TestApiKey1,
                AuthSource = "opencode-auth",
            },
        };

        ProviderMetadataCatalog.NormalizeProviderConfigurations(configs);

        var codingPlan = Assert.Single(configs);
        Assert.Equal("minimax-coding-plan", codingPlan.ProviderId); // provider-id-guardrail-allow: test assertion
        Assert.Equal(TestApiKey1, codingPlan.ApiKey);
        Assert.Equal("opencode-auth", codingPlan.AuthSource);
    }

    [Fact]
    public void NormalizeProviderConfigurations_KeepsMinimaxIoAsDedicatedProvider()
    {
        // minimax-io must NOT be merged into the minimax (China) config.
        var configs = new List<ProviderConfig>
        {
            new()
            {
                ProviderId = "minimax-io",
                ApiKey = TestApiKey2,
            },
        };

        ProviderMetadataCatalog.NormalizeProviderConfigurations(configs);

        var international = Assert.Single(configs);
        Assert.Equal("minimax-io", international.ProviderId); // provider-id-guardrail-allow: test assertion
        Assert.Equal(TestApiKey2, international.ApiKey);
    }

    [Fact]
    public void NormalizeProviderConfigurations_DoesNotMergeOpenAiIntoCodex_WhenBothExist()
    {
        var configs = new List<ProviderConfig>
        {
            new() { ProviderId = "codex", ApiKey = TestApiKey3 },
            new() { ProviderId = "openai", ApiKey = TestApiKey4 },
        };

        ProviderMetadataCatalog.NormalizeProviderConfigurations(configs);

        Assert.Equal(2, configs.Count);
        var codex = Assert.Single(configs.Where(c => string.Equals(c.ProviderId, "codex", StringComparison.OrdinalIgnoreCase)));
        var openAi = Assert.Single(configs.Where(c => string.Equals(c.ProviderId, "openai", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(TestApiKey3, codex.ApiKey);
        Assert.Equal(TestApiKey4, openAi.ApiKey);
    }
}
