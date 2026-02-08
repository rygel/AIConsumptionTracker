using System.Reflection;
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
            
            // Tag usually "v1.7.3" -> "1.7.3"
            var tagVersionStr = release.TagName.StartsWith("v") ? release.TagName[1..] : release.TagName;
            
            if (Version.TryParse(tagVersionStr, out var latestVersion))
            {
                if (latestVersion > currentVersion)
                {
                    _logger.LogInformation($"New version found: {latestVersion} (Current: {currentVersion})");
                    
                    var exeAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe"));
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
}
