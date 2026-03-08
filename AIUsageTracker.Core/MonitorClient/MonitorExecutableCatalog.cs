namespace AIUsageTracker.Core.MonitorClient;

public static class MonitorExecutableCatalog
{
    public static IReadOnlyList<string> GetExecutableCandidates(string baseDirectory, string monitorExecutableName)
    {
        return new[]
        {
            Path.Combine(baseDirectory, "..", "..", "..", "..", "AIUsageTracker.Monitor", "bin", "Debug", "net8.0", monitorExecutableName),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "AIUsageTracker.Monitor", "bin", "Release", "net8.0", monitorExecutableName),
            Path.Combine(baseDirectory, monitorExecutableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AIUsageTracker", monitorExecutableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIUsageTracker", monitorExecutableName),
        };
    }
}
