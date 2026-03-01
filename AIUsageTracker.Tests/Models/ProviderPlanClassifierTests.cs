using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Tests.Models;

public class ProviderPlanClassifierTests
{
    [Theory]
    [InlineData("codex", true)]
    [InlineData("codex.spark", true)]
    [InlineData("antigravity", true)]
    [InlineData("antigravity.gemini-3-flash", true)]
    [InlineData("openrouter", false)]
    [InlineData("openrouter.some-child", false)]
    public void IsCodingPlanProvider_ClassifiesExpectedProviders(string providerId, bool expected)
    {
        var actual = ProviderPlanClassifier.IsCodingPlanProvider(providerId);
        Assert.Equal(expected, actual);
    }
}
