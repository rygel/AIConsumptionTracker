namespace AIUsageTracker.Core.MonitorClient;

public static class MonitorInfoPathCatalog
{
    public static IReadOnlyList<string> GetWriteCandidatePaths(string appDataRoot, string userProfileRoot)
    {
        return BuildCandidatePaths(appDataRoot, userProfileRoot, includeLegacyCompatibilityPaths: false);
    }

    public static IReadOnlyList<string> GetReadCandidatePaths(string appDataRoot, string userProfileRoot)
    {
        return BuildCandidatePaths(appDataRoot, userProfileRoot, includeLegacyCompatibilityPaths: true);
    }

    private static IReadOnlyList<string> BuildCandidatePaths(
        string appDataRoot,
        string userProfileRoot,
        bool includeLegacyCompatibilityPaths)
    {
        var paths = new List<string>
        {
            Path.Combine(appDataRoot, "AIUsageTracker", "monitor.json"),
            Path.Combine(appDataRoot, "AIUsageTracker", "Monitor", "monitor.json"),
            Path.Combine(appDataRoot, "AIUsageTracker", "Agent", "monitor.json"),
            Path.Combine(userProfileRoot, ".ai-consumption-tracker", "monitor.json"),
            Path.Combine(userProfileRoot, ".opencode", "monitor.json")
        };

        if (includeLegacyCompatibilityPaths)
        {
            paths.Add(Path.Combine(appDataRoot, "AIConsumptionTracker", "monitor.json"));
            paths.Add(Path.Combine(appDataRoot, "AIConsumptionTracker", "Monitor", "monitor.json"));
            paths.Add(Path.Combine(appDataRoot, "AIConsumptionTracker", "Agent", "monitor.json"));
        }

        return paths;
    }
}
