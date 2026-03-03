using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Infrastructure.Services;
using Xunit;

namespace AIUsageTracker.Tests.Infrastructure.Services;

public class AuthFileLocatorTests
{
    private readonly IAuthFileLocator _authFileLocator;

    public AuthFileLocatorTests()
    {
        _authFileLocator = new AuthFileLocator();
    }

    [Fact]
    public void GetCodexAuthFileCandidates_WithExplicitPath_ReturnsOnlyExplicitPath()
    {
        // Arrange
        var explicitPath = @"C:\custom\path\auth.json";

        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates(explicitPath).ToList();

        // Assert
        Assert.Single(candidates);
        Assert.Equal(explicitPath, candidates[0]);
    }

    [Fact]
    public void GetCodexAuthFileCandidates_WithNull_ReturnsMultipleCandidates()
    {
        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates(null).ToList();

        // Assert
        Assert.True(candidates.Count >= 3);
        Assert.All(candidates, path => Assert.False(string.IsNullOrWhiteSpace(path)));
    }

    [Fact]
    public void GetCodexAuthFileCandidates_WithEmptyString_ReturnsMultipleCandidates()
    {
        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates(string.Empty).ToList();

        // Assert
        Assert.True(candidates.Count >= 3);
        Assert.All(candidates, path => Assert.False(string.IsNullOrWhiteSpace(path)));
    }

    [Fact]
    public void GetCodexAuthFileCandidates_WithWhitespace_ReturnsMultipleCandidates()
    {
        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates("   ").ToList();

        // Assert
        Assert.True(candidates.Count >= 3);
        Assert.All(candidates, path => Assert.False(string.IsNullOrWhiteSpace(path)));
    }

    [Fact]
    public void GetCodexAuthFileCandidates_DefaultPaths_ContainExpectedPaths()
    {
        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates().ToList();

        // Assert
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Contains(candidates, p => p.Contains(".codex"));
        Assert.Contains(candidates, p => p.Contains("opencode"));
        Assert.All(candidates, p => Assert.True(p.EndsWith("auth.json")));
    }

    [Theory]
    [InlineData("test.json")]
    [InlineData("/home/user/auth.json")]
    [InlineData("relative/path.json")]
    public void GetCodexAuthFileCandidates_WithVariousExplicitPaths_ReturnsOnlyThatPath(string path)
    {
        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates(path).ToList();

        // Assert
        Assert.Single(candidates);
        Assert.Equal(path, candidates[0]);
    }

    [Fact]
    public void GetCodexAuthFileCandidates_OnWindows_ContainsAppDataPaths()
    {
        // Skip on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Act
        var candidates = _authFileLocator.GetCodexAuthFileCandidates().ToList();

        // Assert
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Contains(candidates, p => p.Contains(appData));
        Assert.Contains(candidates, p => p.Contains(localAppData));
    }
}
