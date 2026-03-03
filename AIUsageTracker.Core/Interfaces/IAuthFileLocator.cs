namespace AIUsageTracker.Core.Interfaces;

/// <summary>
/// Service for locating authentication files across different providers and platforms.
/// </summary>
public interface IAuthFileLocator
{
    /// <summary>
    /// Gets candidate file paths for Codex/OpenCode authentication files.
    /// </summary>
    /// <param name="explicitPath">Optional explicit path to check first. If provided and valid, only this path is returned.</param>
    /// <returns>An enumerable of candidate file paths in order of preference.</returns>
    IEnumerable<string> GetCodexAuthFileCandidates(string? explicitPath = null);
}
