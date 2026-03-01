using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Tests.Models;

public class ProviderDisplayNameResolverTests
{
    [Theory]
    [InlineData("openai", "OpenAI", "OpenAI")]
    [InlineData("codex", "Codex", "OpenAI (Codex)")]
    [InlineData("codex.spark", "Codex Spark", "OpenAI (GPT-5.3-Codex-Spark)")]
    [InlineData("github-copilot", "GitHub", "GitHub Copilot")]
    public void GetDisplayName_KnownProvider_UsesCentralMapping(string providerId, string providerName, string expected)
    {
        var actual = ProviderDisplayNameResolver.GetDisplayName(providerId, providerName);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetDisplayName_UnknownProvider_PrefersProviderNameFromPayload()
    {
        var actual = ProviderDisplayNameResolver.GetDisplayName("custom-provider", "Custom Provider Label");

        Assert.Equal("Custom Provider Label", actual);
    }

    [Fact]
    public void GetDisplayName_UnknownProviderWithoutPayload_HumanizesProviderId()
    {
        var actual = ProviderDisplayNameResolver.GetDisplayName("custom-provider");

        Assert.Equal("Custom Provider", actual);
    }
}
