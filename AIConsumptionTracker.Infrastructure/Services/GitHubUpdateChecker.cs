using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Octokit;
using AIConsumptionTracker.Core.Interfaces;

namespace AIConsumptionTracker.Infrastructure.Services;

public class GitHubUpdateChecker : IUpdateCheckerService
{
    private readonly ILogger<GitHubUpdateChecker> _logger;
    private const string REPO_OWNER = "rygel";
    private const string REPO_NAME = "AIConsumptionTracker";

    public GitHubUpdateChecker(ILogger<GitHubUpdateChecker> logger)
    {
        _logger = logger;
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("AIConsumptionTracker"));
            var release = await client.Repository.Release.GetLatest(REPO_OWNER, REPO_NAME);

            var currentVersion = Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(1, 0, 0);
            
            if (IsUpdateAvailable(currentVersion, release.TagName, out var latestVersion))
            {
                if (latestVersion! > currentVersion)
                {
                    _logger.LogInformation($"New version found: {latestVersion} (Current: {currentVersion})");
                    
                    var exeAsset = GetCorrectArchitectureAsset(release.Assets);
                    var downloadUrl = exeAsset?.BrowserDownloadUrl ?? release.HtmlUrl;

                    return new UpdateInfo
                    {
                        Version = release.TagName,
                        ReleaseUrl = release.HtmlUrl,
                        DownloadUrl = downloadUrl,
                        ReleaseNotes = release.Body,
                        PublishedAt = release.PublishedAt?.DateTime ?? DateTime.Now
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
        }
        
        return null;
    }

    public static bool IsUpdateAvailable(Version current, string tagName, out Version? parsedLatest)
    {
        parsedLatest = null;
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        // Tag usually "v1.7.3" -> "1.7.3"
        var tagVersionStr = tagName.StartsWith("v") ? tagName[1..] : tagName;

        if (Version.TryParse(tagVersionStr, out var latestVersion))
        {
            parsedLatest = latestVersion;
            // Strict check: latest > current
            return latestVersion > current;
        }
        
        return false;
    }

    private static ReleaseAsset? GetCorrectArchitectureAsset(IReadOnlyList<ReleaseAsset> assets)
    {
        var architecture = GetWindowsArchitecture();
        var archSuffix = architecture.ToString().ToLowerInvariant();
        
        return assets.FirstOrDefault(a => a.Name.EndsWith(".exe") && a.Name.Contains(archSuffix));
    }

    private static string GetWindowsArchitecture()
    {
        // Check if we're running as a 32-bit process on a 64-bit OS
        if (System.Environment.Is64BitOperatingSystem && !System.IntPtr.Size.Equals(8))
        {
            return "x86";
        }
        
        // Check process architecture directly
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return arch switch
        {
            "x86" or "arm" => "x86",
            "x64" or "arm64" => "x64",
            _ => "x64"
        };
    }
}
