using System.Reflection;
using AIUsageTracker.Core.Models;
using Xunit;

namespace AIUsageTracker.Tests.UI;

public class DualProgressBarLogicTests
{
    // We use reflection to test the private/static methods in MainWindow without a UI thread
    private static readonly MethodInfo? ParseMethod = typeof(AIUsageTracker.UI.Slim.MainWindow)
        .GetMethod("ParseUsedPercentFromDetail", BindingFlags.Static | BindingFlags.NonPublic);

    [Theory]
    [InlineData("10% used", 10.0)]
    [InlineData("45.5 % used", 45.5)]
    [InlineData("100% used", 100.0)]
    [InlineData("0% used", 0.0)]
    [InlineData("80% remaining", 20.0)]
    [InlineData("25.5 % remaining", 74.5)]
    [InlineData("0% remaining", 100.0)]
    [InlineData("100% remaining", 0.0)]
    [InlineData("50%", 50.0)] // Fallback
    [InlineData("Invalid", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseUsedPercentFromDetail_HandlesFormatsCorrectly(string? input, double? expected)
    {
        Assert.NotNull(ParseMethod);
        var result = (double?)ParseMethod!.Invoke(null, new object?[] { input });
        
        if (expected == null)
            Assert.Null(result);
        else
            Assert.Equal(expected.Value, result!.Value, 1);
    }

    [Fact]
    public void TryGetDualWindowUsedPercentages_IdentifiesPrimaryAndSecondary()
    {
        var method = typeof(AIUsageTracker.UI.Slim.MainWindow)
            .GetMethod("TryGetDualWindowUsedPercentages", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var usage = new ProviderUsage
        {
            Details = new List<ProviderUsageDetail>
            {
                new ProviderUsageDetail 
                { 
                    Name = "Hourly", 
                    Used = "10% used", 
                    DetailType = ProviderUsageDetailType.QuotaWindow, 
                    WindowKind = WindowKind.Primary 
                },
                new ProviderUsageDetail 
                { 
                    Name = "Weekly", 
                    Used = "80% remaining", 
                    DetailType = ProviderUsageDetailType.QuotaWindow, 
                    WindowKind = WindowKind.Secondary 
                }
            }
        };

        var args = new object?[] { usage, 0.0, 0.0 };
        var result = (bool)method!.Invoke(null, args)!;

        Assert.True(result);
        Assert.Equal(10.0, (double)args[1]!);
        Assert.Equal(20.0, (double)args[2]!); // 100 - 80
    }
}
