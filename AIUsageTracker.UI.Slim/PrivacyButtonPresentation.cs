namespace AIUsageTracker.UI.Slim;

public sealed record PrivacyButtonPresentation(
    string IconGlyph,
    PrivacyButtonForegroundKind ForegroundKind);

public enum PrivacyButtonForegroundKind
{
    SecondaryText,
    Highlight,
}
