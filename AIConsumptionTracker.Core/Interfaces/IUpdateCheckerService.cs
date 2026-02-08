namespace AIConsumptionTracker.Core.Interfaces;

public interface IUpdateCheckerService
{
    Task<UpdateInfo?> CheckForUpdatesAsync();
}

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}
