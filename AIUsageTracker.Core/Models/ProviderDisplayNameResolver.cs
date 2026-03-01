using System.Globalization;

namespace AIUsageTracker.Core.Models;

public static class ProviderDisplayNameResolver
{
    private static readonly IReadOnlyDictionary<string, string> KnownProviderDisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["antigravity"] = "Google Antigravity",
            ["gemini-cli"] = "Google Gemini",
            ["github-copilot"] = "GitHub Copilot",
            ["openai"] = "OpenAI",
            ["codex"] = "OpenAI (Codex)",
            ["codex.spark"] = "OpenAI (GPT-5.3-Codex-Spark)",
            ["minimax"] = "Minimax (China)",
            ["minimax-io"] = "Minimax (International)",
            ["opencode"] = "OpenCode",
            ["opencode-zen"] = "OpenCode Zen",
            ["claude-code"] = "Claude Code",
            ["openrouter"] = "OpenRouter",
            ["zai-coding-plan"] = "Z.ai Coding Plan",
            ["zai"] = "Z.AI",
            ["deepseek"] = "DeepSeek",
            ["synthetic"] = "Synthetic",
            ["xiaomi"] = "Xiaomi"
        };

    public static string GetDisplayName(string providerId, string? providerName = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return providerName ?? string.Empty;
        }

        if (KnownProviderDisplayNames.TryGetValue(providerId, out var mappedName))
        {
            return mappedName;
        }

        if (!string.IsNullOrWhiteSpace(providerName) &&
            !providerName.Equals(providerId, StringComparison.OrdinalIgnoreCase))
        {
            return providerName;
        }

        if (providerId.Contains('.'))
        {
            return providerName ?? providerId;
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(providerId.Replace("_", " ").Replace("-", " "));
    }
}
