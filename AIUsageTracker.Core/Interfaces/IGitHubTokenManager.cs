using System.Threading.Tasks;

namespace AIUsageTracker.Core.Interfaces;

/// <summary>
/// Interface for GitHub token management operations.
/// Handles token retrieval, refresh, and logout.
/// </summary>
public interface IGitHubTokenManager
{
    /// <summary>
    /// Gets the currently authenticated access token, if any.
    /// </summary>
    /// <returns>The access token or null if not authenticated.</returns>
    string? GetCurrentToken();
    
    /// <summary>
    /// Refreshes the access token (though for VS Code device flow tokens, refresh is typically a no-op).
    /// </summary>
    /// <returns>A task that completes when refresh is done.</returns>
    Task RefreshTokenAsync();
    
    /// <summary>
    /// Logs out by clearing the stored token.
    /// </summary>
    void Logout();
    
    /// <summary>
    /// Checks if the user is currently authenticated.
    /// </summary>
    /// <returns>True if authenticated, false otherwise.</returns>
    bool IsAuthenticated { get; }
}
