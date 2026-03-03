using AIUsageTracker.Core.Interfaces;

namespace AIUsageTracker.Infrastructure.Services;

/// <summary>
/// Implementation of IAuthFileLocator that provides cross-platform auth file path resolution.
/// </summary>
public class AuthFileLocator : IAuthFileLocator
{
    /// <inheritdoc />
    public IEnumerable<string> GetCodexAuthFileCandidates(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return explicitPath;
            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".codex", "auth.json");
        yield return Path.Combine(home, ".local", "share", "opencode", "auth.json");
        yield return Path.Combine(home, ".opencode", "auth.json");

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                yield return Path.Combine(appData, "codex", "auth.json");
                yield return Path.Combine(appData, "opencode", "auth.json");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                yield return Path.Combine(localAppData, "opencode", "auth.json");
            }
        }
    }
}
