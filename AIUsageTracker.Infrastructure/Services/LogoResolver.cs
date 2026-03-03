using System.IO;
using AIUsageTracker.Core.Services;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.Infrastructure.Services;

/// <summary>
/// Implementation of ILogoResolver that maps provider IDs to logo files and fallback icons.
/// </summary>
public class LogoResolver : ILogoResolver
{
    public string? GetLogoFilename(string providerId)
    {
        var normalizedId = providerId.ToLowerInvariant();

        // First check ProviderMetadataCatalog for explicit logo key
        if (ProviderMetadataCatalog.TryGet(normalizedId, out var definition) &&
            !string.IsNullOrEmpty(definition.LogoKey))
        {
            return definition.LogoKey.ToLowerInvariant();
        }

        // Otherwise use built-in mapping
        return normalizedId switch
        {
            "github-copilot" => "github",
            "gemini-cli" or "antigravity" => "google",
            "claude-code" or "claude" => "anthropic",
            "minimax" or "minimax-io" or "minimax-global" => "minimax",
            "codex" or "codex.spark" => "openai",
            "openrouter" => "openai",
            "kimi" => "kimi",
            "xiaomi" => "xiaomi",
            "zai" or "zai-coding-plan" => "zai",
            "deepseek" => "deepseek",
            "mistral" => "mistral",
            "openai" => "openai",
            "anthropic" => "anthropic",
            "google" => "google",
            "github" => "github",
            _ => normalizedId
        };
    }

    public (string BrushName, string Initials) GetFallbackIconData(string providerId)
    {
        return providerId.ToLowerInvariant() switch
        {
            "antigravity" or "google" or "gemini" => ("RoyalBlue", "G"),
            "openai" or "codex" => ("DarkCyan", "AI"),
            "anthropic" or "claude" or "claude-code" => ("IndianRed", "An"),
            "github-copilot" or "github" => ("MediumPurple", "GH"),
            "mistral" => ("Orange", "M"),
            "deepseek" => ("DeepSkyBlue", "DS"),
            "kimi" => ("Teal", "K"),
            "zai" or "zai-coding-plan" => ("SlateBlue", "Z"),
            _ => ("Gray", "?")
        };
    }

    public string? GetLogoPath(string providerId)
    {
        var filename = GetLogoFilename(providerId);
        if (string.IsNullOrEmpty(filename))
            return null;

        var svgPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets", "ProviderLogos", $"{filename}.svg");

        if (File.Exists(svgPath))
            return svgPath;

        var icoPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets", "ProviderLogos", $"{filename}.ico");

        if (File.Exists(icoPath))
            return icoPath;

        return null;
    }
}
