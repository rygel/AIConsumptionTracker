namespace AIUsageTracker.UI.Slim;

public static class PrivacyButtonPresentationCatalog
{
    public static PrivacyButtonPresentation Create(bool isPrivacyMode)
    {
        return isPrivacyMode
            ? new PrivacyButtonPresentation("\uE72E", PrivacyButtonForegroundKind.Highlight)
            : new PrivacyButtonPresentation("\uE785", PrivacyButtonForegroundKind.SecondaryText);
    }
}
