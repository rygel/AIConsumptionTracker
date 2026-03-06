using System.Reflection;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.Tests.Infrastructure;

public class ProviderMetadataCatalogTests
{
    [Fact]
    public void Definitions_AreDiscoveredFromProviderClasses()
    {
        var expectedProviderIds = typeof(ProviderMetadataCatalog).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                typeof(IProviderService).IsAssignableFrom(type))
            .Select(type => type.GetProperty(
                "StaticDefinition",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Select(property => Assert.IsType<ProviderDefinition>(property?.GetValue(null)))
            .Select(definition => definition.ProviderId)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var actualProviderIds = ProviderMetadataCatalog.Definitions
            .Select(definition => definition.ProviderId)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(expectedProviderIds, actualProviderIds);
    }

    [Theory]
    [InlineData("codex.spark", "codex", "OpenAI (GPT-5.3-Codex-Spark)")]
    [InlineData("gemini", "gemini-cli", "Google Gemini")]
    [InlineData("minimax-io", "minimax", "Minimax (International)")]
    [InlineData("minimax-global", "minimax", "Minimax (International)")]
    [InlineData("zai", "zai-coding-plan", "Z.AI")]
    public void Find_UsesProviderDefinitionsForAliases(string providerId, string expectedDefinitionId, string expectedDisplayName)
    {
        var definition = ProviderMetadataCatalog.Find(providerId);

        Assert.NotNull(definition);
        Assert.Equal(expectedDefinitionId, definition!.ProviderId);
        Assert.Equal(expectedDisplayName, ProviderMetadataCatalog.GetDisplayName(providerId));
    }

    [Fact]
    public void Definitions_DoNotExposeDuplicateHandledProviderIds()
    {
        var duplicateHandledIds = ProviderMetadataCatalog.Definitions
            .SelectMany(definition => definition.HandledProviderIds.Select(handledId => new
            {
                HandledId = handledId,
                definition.ProviderId
            }))
            .GroupBy(item => item.HandledId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.ProviderId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicateHandledIds);
    }

    [Fact]
    public void TryCreateDefaultConfig_UsesDefinitionDefaults()
    {
        var created = ProviderMetadataCatalog.TryCreateDefaultConfig(
            "codex.spark",
            out var config,
            apiKey: "token",
            authSource: "test",
            description: "demo");

        Assert.True(created);
        Assert.Equal("codex.spark", config.ProviderId);
        Assert.Equal("quota-based", config.Type);
        Assert.Equal(PlanType.Coding, config.PlanType);
        Assert.Equal("token", config.ApiKey);
        Assert.Equal("test", config.AuthSource);
        Assert.Equal("demo", config.Description);
    }

    [Theory]
    [InlineData("antigravity", true)]
    [InlineData("antigravity.some-model", true)]
    [InlineData("codex", false)]
    [InlineData("openrouter", false)]
    public void IsAutoIncluded_UsesProviderDefinitions(string providerId, bool expected)
    {
        Assert.Equal(expected, ProviderMetadataCatalog.IsAutoIncluded(providerId));
    }

    [Theory]
    [InlineData("codex.spark", "codex")]
    [InlineData("antigravity.claude-opus", "antigravity")]
    [InlineData("minimax-io", "minimax")]
    [InlineData("unknown-provider", "unknown-provider")]
    public void GetCanonicalProviderId_UsesProviderDefinitions(string providerId, string expectedCanonicalId)
    {
        Assert.Equal(expectedCanonicalId, ProviderMetadataCatalog.GetCanonicalProviderId(providerId));
    }
}
