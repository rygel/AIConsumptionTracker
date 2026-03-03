using System.Threading.Tasks;

namespace AIUsageTracker.Core.Interfaces;

/// <summary>
/// Interface for GitHub Device Flow operations.
/// Handles device code initialization and token polling.
/// </summary>
public interface IGitHubDeviceFlow
{
    /// <summary>
    /// Initiates Device Flow and returns device code, user code, and verification URI.
    /// </summary>
    /// <param name="deviceCode">Optional device code to use for authentication.</param>
    /// <returns>A task containing device code, user code, verification URI, expiry time, and polling interval.</returns>
    Task<(string deviceCode, string userCode, string verificationUri, int expiresIn, int interval)> InitiateDeviceFlowAsync(string? deviceCode = null);
    
    /// <summary>
    /// Polls GitHub for access token using device code.
    /// </summary>
    /// <param name="deviceCode">Device code from the Device Flow.</param>
    /// <param name="interval">Polling interval in milliseconds.</param>
    /// <returns>A task that resolves to the access token or null if polling is not needed.</returns>
    Task<string?> PollForTokenAsync(string deviceCode, int interval);
}
