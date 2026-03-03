using System.Threading.Tasks;

namespace AIUsageTracker.Core.Interfaces;

/// <summary>
/// Interface for GitHub user profile operations.
/// </summary>
public interface IGitHubUserProvider
{
    /// <summary>
    /// Gets the username of the authenticated user.
    /// </summary>
    /// <returns>A task that resolves to the username or null if not authenticated.</returns>
    Task<string?> GetUsernameAsync();
}
