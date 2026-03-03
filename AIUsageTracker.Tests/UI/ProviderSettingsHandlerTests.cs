using System.Windows;
using AIUsageTracker.Core.Models;
using AIUsageTracker.UI.Slim.Services;
using Xunit;

namespace AIUsageTracker.Tests.UI;

/// <summary>
/// Unit tests for provider settings handlers.
/// Tests the logic without creating WPF UI elements.
/// </summary>
public class ProviderSettingsHandlerTests
{
    #region DefaultProviderSettingsHandler Tests

    [Fact]
    public void DefaultHandler_IsInactive_NoApiKey_ReturnsTrue()
    {
        var handler = new DefaultProviderSettingsHandler();
        var config = new ProviderConfig { ProviderId = "test", ApiKey = null };
        
        var result = handler.IsInactive(config, null);
        
        Assert.True(result);
    }

    [Fact]
    public void DefaultHandler_IsInactive_WithApiKey_ReturnsFalse()
    {
        var handler = new DefaultProviderSettingsHandler();
        var config = new ProviderConfig { ProviderId = "test", ApiKey = "secret-key" };
        
        var result = handler.IsInactive(config, null);
        
        Assert.False(result);
    }

    [Fact]
    public void DefaultHandler_ProviderId_IsDefault()
    {
        var handler = new DefaultProviderSettingsHandler();
        
        Assert.Equal("default", handler.ProviderId);
    }

    #endregion

    #region AntigravitySettingsHandler Tests

    [Fact]
    public void AntigravityHandler_IsInactive_NoUsage_ReturnsTrue()
    {
        var handler = new AntigravitySettingsHandler();
        var config = new ProviderConfig { ProviderId = "antigravity" };
        
        var result = handler.IsInactive(config, null);
        
        Assert.True(result);
    }

    [Fact]
    public void AntigravityHandler_IsInactive_NotAvailable_ReturnsTrue()
    {
        var handler = new AntigravitySettingsHandler();
        var config = new ProviderConfig { ProviderId = "antigravity" };
        var usage = new ProviderUsage { ProviderId = "antigravity", IsAvailable = false };
        
        var result = handler.IsInactive(config, usage);
        
        Assert.True(result);
    }

    [Fact]
    public void AntigravityHandler_IsInactive_Available_ReturnsFalse()
    {
        var handler = new AntigravitySettingsHandler();
        var config = new ProviderConfig { ProviderId = "antigravity" };
        var usage = new ProviderUsage { ProviderId = "antigravity", IsAvailable = true };
        
        var result = handler.IsInactive(config, usage);
        
        Assert.False(result);
    }

    [Fact]
    public void AntigravityHandler_ProviderId_IsAntigravity()
    {
        var handler = new AntigravitySettingsHandler();
        
        Assert.Equal("antigravity", handler.ProviderId);
    }

    #endregion

    #region OpenAISettingsHandler Tests

    [Fact]
    public void OpenAIHandler_IsInactive_NoApiKeyNoSessionUsage_ReturnsTrue()
    {
        var handler = new OpenAISettingsHandler();
        var config = new ProviderConfig { ProviderId = "openai", ApiKey = null };
        
        var result = handler.IsInactive(config, null);
        
        Assert.True(result);
    }

    [Fact]
    public void OpenAIHandler_IsInactive_WithApiKey_ReturnsFalse()
    {
        var handler = new OpenAISettingsHandler();
        var config = new ProviderConfig { ProviderId = "openai", ApiKey = "sk-..." };
        
        var result = handler.IsInactive(config, null);
        
        Assert.False(result);
    }

    [Fact]
    public void OpenAIHandler_IsInactive_WithSessionUsage_ReturnsFalse()
    {
        var handler = new OpenAISettingsHandler();
        var config = new ProviderConfig { ProviderId = "openai", ApiKey = null };
        var usage = new ProviderUsage 
        { 
            ProviderId = "openai", 
            IsAvailable = true,
            IsQuotaBased = true
        };
        
        var result = handler.IsInactive(config, usage);
        
        Assert.False(result);
    }

    [Fact]
    public void OpenAIHandler_IsInactive_SessionUsageNotQuotaBased_ReturnsTrue()
    {
        var handler = new OpenAISettingsHandler();
        var config = new ProviderConfig { ProviderId = "openai", ApiKey = null };
        var usage = new ProviderUsage 
        { 
            ProviderId = "openai", 
            IsAvailable = true,
            IsQuotaBased = false
        };
        
        var result = handler.IsInactive(config, usage);
        
        Assert.True(result);
    }

    [Fact]
    public void OpenAIHandler_IsInactive_SessionUsageNotAvailable_ReturnsTrue()
    {
        var handler = new OpenAISettingsHandler();
        var config = new ProviderConfig { ProviderId = "openai", ApiKey = null };
        var usage = new ProviderUsage 
        { 
            ProviderId = "openai", 
            IsAvailable = false,
            IsQuotaBased = true
        };
        
        var result = handler.IsInactive(config, usage);
        
        Assert.True(result);
    }

    [Fact]
    public void OpenAIHandler_ProviderId_IsOpenAI()
    {
        var handler = new OpenAISettingsHandler();
        
        Assert.Equal("openai", handler.ProviderId);
    }

    #endregion

    #region GitHubCopilotSettingsHandler Tests

    [Fact]
    public void GitHubCopilotHandler_IsInactive_NoApiKey_ReturnsTrue()
    {
        var handler = new GitHubCopilotSettingsHandler();
        var config = new ProviderConfig { ProviderId = "github-copilot", ApiKey = null };
        
        var result = handler.IsInactive(config, null);
        
        Assert.True(result);
    }

    [Fact]
    public void GitHubCopilotHandler_IsInactive_WithApiKey_ReturnsFalse()
    {
        var handler = new GitHubCopilotSettingsHandler();
        var config = new ProviderConfig { ProviderId = "github-copilot", ApiKey = "token" };
        
        var result = handler.IsInactive(config, null);
        
        Assert.False(result);
    }

    [Fact]
    public void GitHubCopilotHandler_ProviderId_IsGitHubCopilot()
    {
        var handler = new GitHubCopilotSettingsHandler();
        
        Assert.Equal("github-copilot", handler.ProviderId);
    }

    #endregion

    #region CodexSettingsHandler Tests

    [Fact]
    public void CodexHandler_IsInactive_NoApiKey_ReturnsTrue()
    {
        var handler = new CodexSettingsHandler();
        var config = new ProviderConfig { ProviderId = "codex", ApiKey = null };
        
        var result = handler.IsInactive(config, null);
        
        Assert.True(result);
    }

    [Fact]
    public void CodexHandler_IsInactive_WithApiKey_ReturnsFalse()
    {
        var handler = new CodexSettingsHandler();
        var config = new ProviderConfig { ProviderId = "codex", ApiKey = "token" };
        
        var result = handler.IsInactive(config, null);
        
        Assert.False(result);
    }

    [Fact]
    public void CodexHandler_ProviderId_IsCodex()
    {
        var handler = new CodexSettingsHandler();
        
        Assert.Equal("codex", handler.ProviderId);
    }

    #endregion

    #region ProviderSettingsHandlerFactory Tests

    [Fact]
    public void Factory_GetHandler_Antigravity_ReturnsAntigravityHandler()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("antigravity");
        
        Assert.IsType<AntigravitySettingsHandler>(handler);
        Assert.Equal("antigravity", handler.ProviderId);
    }

    [Fact]
    public void Factory_GetHandler_OpenAI_ReturnsOpenAIHandler()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("openai");
        
        Assert.IsType<OpenAISettingsHandler>(handler);
        Assert.Equal("openai", handler.ProviderId);
    }

    [Fact]
    public void Factory_GetHandler_GitHubCopilot_ReturnsGitHubCopilotHandler()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("github-copilot");
        
        Assert.IsType<GitHubCopilotSettingsHandler>(handler);
        Assert.Equal("github-copilot", handler.ProviderId);
    }

    [Fact]
    public void Factory_GetHandler_Codex_ReturnsCodexHandler()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("codex");
        
        Assert.IsType<CodexSettingsHandler>(handler);
        Assert.Equal("codex", handler.ProviderId);
    }

    [Fact]
    public void Factory_GetHandler_UnknownProvider_ReturnsDefaultHandler()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("unknown-provider");
        
        Assert.IsType<DefaultProviderSettingsHandler>(handler);
        Assert.Equal("default", handler.ProviderId);
    }

    [Fact]
    public void Factory_GetHandler_CaseInsensitive_Uppercase()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("ANTIGRAVITY");
        
        Assert.IsType<AntigravitySettingsHandler>(handler);
    }

    [Fact]
    public void Factory_GetHandler_CaseInsensitive_MixedCase()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("Antigravity");
        
        Assert.IsType<AntigravitySettingsHandler>(handler);
    }

    [Fact]
    public void Factory_GetHandler_CaseInsensitive_Lowercase()
    {
        var handler = ProviderSettingsHandlerFactory.GetHandler("antigravity");
        
        Assert.IsType<AntigravitySettingsHandler>(handler);
    }

    [Fact]
    public void Factory_RegisterHandler_CustomProvider_ReturnsRegisteredHandler()
    {
        var customHandler = new CustomTestHandler();
        ProviderSettingsHandlerFactory.RegisterHandler("custom-test", customHandler);
        
        var handler = ProviderSettingsHandlerFactory.GetHandler("custom-test");
        
        Assert.Same(customHandler, handler);
        Assert.Equal("custom-test", handler.ProviderId);
    }

    [Fact]
    public void Factory_RegisterHandler_OverridesExisting()
    {
        var customHandler = new CustomTestHandler();
        ProviderSettingsHandlerFactory.RegisterHandler("antigravity", customHandler);
        
        var handler = ProviderSettingsHandlerFactory.GetHandler("antigravity");
        
        Assert.Same(customHandler, handler);
        
        // Restore original handler
        ProviderSettingsHandlerFactory.RegisterHandler("antigravity", new AntigravitySettingsHandler());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DefaultHandler_IsInactive_EmptyApiKey_ReturnsTrue()
    {
        var handler = new DefaultProviderSettingsHandler();
        var config = new ProviderConfig { ProviderId = "test", ApiKey = "" };
        
        var result = handler.IsInactive(config, null);
        
        Assert.True(result);
    }

    [Fact]
    public void DefaultHandler_IsInactive_WhitespaceApiKey_ReturnsFalse()
    {
        var handler = new DefaultProviderSettingsHandler();
        var config = new ProviderConfig { ProviderId = "test", ApiKey = "   " };
        
        var result = handler.IsInactive(config, null);
        
        Assert.False(result); // Whitespace is still a value
    }

    [Fact]
    public void OpenAIHandler_IsInactive_EmptyApiKeyWithSessionUsage_ReturnsFalse()
    {
        var handler = new OpenAISettingsHandler();
        var config = new ProviderConfig { ProviderId = "openai", ApiKey = "" };
        var usage = new ProviderUsage 
        { 
            ProviderId = "openai", 
            IsAvailable = true,
            IsQuotaBased = true
        };
        
        var result = handler.IsInactive(config, usage);
        
        Assert.False(result);
    }

    [Fact]
    public void AntigravityHandler_IsInactive_IgnoresApiKey()
    {
        var handler = new AntigravitySettingsHandler();
        var configWithKey = new ProviderConfig { ProviderId = "antigravity", ApiKey = "key" };
        var configWithoutKey = new ProviderConfig { ProviderId = "antigravity", ApiKey = null };
        
        // Both should be inactive if no usage
        Assert.True(handler.IsInactive(configWithKey, null));
        Assert.True(handler.IsInactive(configWithoutKey, null));
        
        // Both should be active if usage available
        var usage = new ProviderUsage { ProviderId = "antigravity", IsAvailable = true };
        Assert.False(handler.IsInactive(configWithKey, usage));
        Assert.False(handler.IsInactive(configWithoutKey, usage));
    }

    #endregion
}

/// <summary>
/// Test helper class for testing custom handler registration
/// </summary>
public class CustomTestHandler : IProviderSettingsHandler
{
    public string ProviderId => "custom-test";

    public bool IsInactive(ProviderConfig config, ProviderUsage? usage)
    {
        return false;
    }

    public UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context)
    {
        return null;
    }
}
