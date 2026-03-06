using System.Windows.Media;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.UI.Slim;

internal static class ProviderVisualCatalog
{
    private static readonly IReadOnlyDictionary<string, string> IconAssetNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["github-copilot"] = "github",
            ["gemini-cli"] = "google",
            ["gemini"] = "google",
            ["antigravity"] = "google",
            ["claude-code"] = "anthropic",
            ["claude"] = "anthropic",
            ["minimax"] = "minimax",
            ["kimi"] = "kimi",
            ["xiaomi"] = "xiaomi",
            ["zai"] = "zai",
            ["zai-coding-plan"] = "zai",
            ["deepseek"] = "deepseek",
            ["openrouter"] = "openai",
            ["codex"] = "openai",
            ["openai"] = "openai",
            ["mistral"] = "mistral",
            ["github"] = "github",
            ["google"] = "google"
        };

    private static readonly IReadOnlyDictionary<string, (Brush Color, string Initial)> FallbackBadges =
        new Dictionary<string, (Brush Color, string Initial)>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = (Brushes.DarkCyan, "AI"),
            ["codex"] = (Brushes.DarkCyan, "AI"),
            ["anthropic"] = (Brushes.IndianRed, "An"),
            ["github-copilot"] = (Brushes.MediumPurple, "GH"),
            ["gemini"] = (Brushes.DodgerBlue, "G"),
            ["gemini-cli"] = (Brushes.DodgerBlue, "G"),
            ["google"] = (Brushes.DodgerBlue, "G"),
            ["antigravity"] = (Brushes.DodgerBlue, "G"),
            ["deepseek"] = (Brushes.DeepSkyBlue, "DS"),
            ["openrouter"] = (Brushes.DarkSlateBlue, "OR"),
            ["kimi"] = (Brushes.MediumOrchid, "K"),
            ["minimax"] = (Brushes.DarkTurquoise, "MM"),
            ["mistral"] = (Brushes.OrangeRed, "Mi"),
            ["xiaomi"] = (Brushes.Orange, "Xi"),
            ["zai"] = (Brushes.LightSeaGreen, "Z"),
            ["zai-coding-plan"] = (Brushes.LightSeaGreen, "Z"),
            ["claude-code"] = (Brushes.Orange, "C"),
            ["claude"] = (Brushes.Orange, "C"),
            ["cloudcode"] = (Brushes.DeepSkyBlue, "CC"),
            ["synthetic"] = (Brushes.Gold, "Sy")
        };

    public static string GetCanonicalProviderId(string providerId)
    {
        return ProviderMetadataCatalog.GetCanonicalProviderId(providerId);
    }

    public static string GetIconAssetName(string providerId)
    {
        var canonicalProviderId = GetCanonicalProviderId(providerId);
        return IconAssetNames.TryGetValue(canonicalProviderId, out var assetName)
            ? assetName
            : canonicalProviderId;
    }

    public static (Brush Color, string Initial) GetFallbackBadge(string providerId, Brush defaultBrush)
    {
        var canonicalProviderId = GetCanonicalProviderId(providerId);
        return FallbackBadges.TryGetValue(canonicalProviderId, out var badge)
            ? badge
            : (defaultBrush, canonicalProviderId[..Math.Min(2, canonicalProviderId.Length)].ToUpperInvariant());
    }
}
