using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.UI.Slim;

internal sealed class MainWindowDeterministicFixtureData
{
    public required AppPreferences Preferences { get; init; }
    public required DateTime LastMonitorUpdate { get; init; }
    public required List<ProviderUsage> Usages { get; init; }
    public double WindowWidth { get; init; } = 460;
}

internal static class MainWindowDeterministicFixture
{
    public static MainWindowDeterministicFixtureData Create()
    {
        var deterministicNow = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Local);

        var quotaUsageSeeds = new (string ProviderId, double RequestsPercentage, double RequestsUsed, double RequestsAvailable, string Description, int ResetHours, string AuthSource)[]
        {
            ("github-copilot", 72.5, 110, 400, "72.5% Remaining", 20, "oauth"),
            ("zai-coding-plan", 82.0, 45, 250, "82.0% Remaining", 12, "api key"),
            ("synthetic", 91.0, 18, 200, "91.0% Remaining", 4, "api key")
        };

        var statusUsageSeeds = new (string ProviderId, string Description, string AuthSource)[]
        {
            ("claude-code", "Connected", "local credentials"),
            ("mistral", "Connected", "api key")
        };

        var usages = new List<ProviderUsage>
        {
            CreateUsage(
                "antigravity",
                requestsPercentage: 60.0,
                requestsUsed: 40,
                requestsAvailable: 100,
                description: "60.0% Remaining",
                authSource: "local app",
                nextResetTime: deterministicNow.AddHours(6),
                details: new List<ProviderUsageDetail>
                {
                    CreateDetail(deterministicNow, "Claude Opus 4.6 (Thinking)", "60%", 10),
                    CreateDetail(deterministicNow, "Claude Sonnet 4.6 (Thinking)", "60%", 10),
                    CreateDetail(deterministicNow, "Gemini 3 Flash", "100%", 6),
                    CreateDetail(deterministicNow, "Gemini 3.1 Pro (High)", "100%", 14),
                    CreateDetail(deterministicNow, "Gemini 3.1 Pro (Low)", "100%", 14),
                    CreateDetail(deterministicNow, "GPT-OSS 120B (Medium)", "60%", 8)
                })
        };

        usages.AddRange(quotaUsageSeeds.Select(seed => CreateUsage(
            seed.ProviderId,
            requestsPercentage: seed.RequestsPercentage,
            requestsUsed: seed.RequestsUsed,
            requestsAvailable: seed.RequestsAvailable,
            description: seed.Description,
            authSource: seed.AuthSource,
            nextResetTime: deterministicNow.AddHours(seed.ResetHours))));

        usages.AddRange(statusUsageSeeds.Select(seed => CreateUsage(
            seed.ProviderId,
            description: seed.Description,
            authSource: seed.AuthSource)));

        return new MainWindowDeterministicFixtureData
        {
            Preferences = new AppPreferences
            {
                AlwaysOnTop = true,
                InvertProgressBar = true,
                InvertCalculations = false,
                ColorThresholdYellow = 60,
                ColorThresholdRed = 80,
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontBold = false,
                FontItalic = false,
                IsPrivacyMode = true
            },
            LastMonitorUpdate = deterministicNow,
            Usages = usages
        };
    }

    private static ProviderUsage CreateUsage(
        string providerId,
        double requestsPercentage = 0,
        double requestsUsed = 0,
        double requestsAvailable = 0,
        string description = "Connected",
        string authSource = "api key",
        bool isAvailable = true,
        DateTime? nextResetTime = null,
        List<ProviderUsageDetail>? details = null)
    {
        var definition = ProviderMetadataCatalog.Find(providerId)
            ?? throw new InvalidOperationException($"Unknown provider id '{providerId}' in deterministic screenshot data.");

        return new ProviderUsage
        {
            ProviderId = providerId,
            ProviderName = ProviderMetadataCatalog.GetDisplayName(providerId),
            IsAvailable = isAvailable,
            IsQuotaBased = definition.IsQuotaBased,
            PlanType = definition.PlanType,
            DisplayAsFraction = definition.IsQuotaBased,
            RequestsPercentage = requestsPercentage,
            RequestsUsed = requestsUsed,
            RequestsAvailable = requestsAvailable,
            Description = description,
            Details = details,
            NextResetTime = nextResetTime,
            AuthSource = authSource
        };
    }

    private static ProviderUsageDetail CreateDetail(DateTime deterministicNow, string name, string used, int resetHours)
    {
        return new ProviderUsageDetail
        {
            Name = name,
            ModelName = name,
            GroupName = "Recommended Group 1",
            Used = used,
            Description = $"{used} remaining",
            NextResetTime = deterministicNow.AddHours(resetHours)
        };
    }
}
