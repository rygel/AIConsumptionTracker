using System.Collections.Generic;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Factory for creating provider-specific settings handlers.
/// Maps provider IDs to their respective handler implementations.
/// </summary>
public class ProviderSettingsHandlerFactory
{
    private static readonly Dictionary<string, IProviderSettingsHandler> _handlers;
    private static readonly IProviderSettingsHandler _defaultHandler;

    static ProviderSettingsHandlerFactory()
    {
        _defaultHandler = new DefaultProviderSettingsHandler();
        
        _handlers = new Dictionary<string, IProviderSettingsHandler>(StringComparer.OrdinalIgnoreCase)
        {
            { "antigravity", new AntigravitySettingsHandler() },
            { "openai", new OpenAISettingsHandler() },
            { "github-copilot", new GitHubCopilotSettingsHandler() },
            { "codex", new CodexSettingsHandler() }
        };
    }

    /// <summary>
    /// Gets the appropriate settings handler for a provider.
    /// Returns the default handler if no specific handler is registered.
    /// </summary>
    /// <param name="providerId">The provider ID</param>
    /// <returns>The settings handler for the provider</returns>
    public static IProviderSettingsHandler GetHandler(string providerId)
    {
        if (_handlers.TryGetValue(providerId, out var handler))
        {
            return handler;
        }
        
        return _defaultHandler;
    }

    /// <summary>
    /// Registers a custom handler for a provider.
    /// Can be used to override default behavior or add new providers.
    /// </summary>
    /// <param name="providerId">The provider ID</param>
    /// <param name="handler">The handler implementation</param>
    public static void RegisterHandler(string providerId, IProviderSettingsHandler handler)
    {
        _handlers[providerId] = handler;
    }
}
