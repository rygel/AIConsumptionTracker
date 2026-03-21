using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public class PrivacyButtonPresentationCatalogTests
{
    [Fact]
    public void Create_WhenPrivacyModeEnabled_ReturnsMaskedIconAndHighlightForeground()
    {
        var result = PrivacyButtonPresentationCatalog.Create(isPrivacyMode: true);

        Assert.Equal("\uE72E", result.IconGlyph);
        Assert.Equal(PrivacyButtonForegroundKind.Highlight, result.ForegroundKind);
    }

    [Fact]
    public void Create_WhenPrivacyModeDisabled_ReturnsOpenIconAndSecondaryForeground()
    {
        var result = PrivacyButtonPresentationCatalog.Create(isPrivacyMode: false);

        Assert.Equal("\uE785", result.IconGlyph);
        Assert.Equal(PrivacyButtonForegroundKind.SecondaryText, result.ForegroundKind);
    }
}
