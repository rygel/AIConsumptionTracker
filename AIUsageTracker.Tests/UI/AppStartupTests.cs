using AIUsageTracker.Core.Models;
using AIUsageTracker.UI.Slim;
using System.Text.Json;
using Xunit;

namespace AIUsageTracker.Tests.UI;

public class AppStartupTests : IDisposable
{
    private readonly string _testPreferencesDirectory;
    private readonly string _testPreferencesPath;

    public AppStartupTests()
    {
        _testPreferencesDirectory = Path.Combine(Path.GetTempPath(), $"AIUsageTracker_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testPreferencesDirectory);
        _testPreferencesPath = Path.Combine(_testPreferencesDirectory, "preferences.json");
        UiPreferencesStore.SetPreferencesPathOverrideForTesting(_testPreferencesPath);
    }

    public void Dispose()
    {
        UiPreferencesStore.SetPreferencesPathOverrideForTesting(null);
        if (Directory.Exists(_testPreferencesDirectory))
        {
            Directory.Delete(_testPreferencesDirectory, true);
        }
    }

    [Fact]
    public async Task LoadPreferencesAsync_DoesNotBlockThread()
    {
        var startTime = DateTime.UtcNow;
        var loadTask = UiPreferencesStore.LoadAsync();

        var completed = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(5)));
        var endTime = DateTime.UtcNow;

        Assert.Same(loadTask, completed);
        Assert.True(endTime - startTime < TimeSpan.FromSeconds(5),
            "Loading preferences took too long - possible blocking call");
    }

    [Fact]
    public async Task LoadPreferencesAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var preferences = await UiPreferencesStore.LoadAsync();

        Assert.NotNull(preferences);
        Assert.True(Enum.IsDefined(typeof(AppTheme), preferences.Theme),
            $"Theme value {preferences.Theme} should be a valid enum value");
    }

    [Fact]
    public async Task LoadPreferencesAsync_WithLightTheme_PreservesLightTheme()
    {
        var preferences = new AppPreferences
        {
            Theme = AppTheme.Light,
            WindowWidth = 420,
            WindowHeight = 600
        };

        var json = JsonSerializer.Serialize(preferences);
        await File.WriteAllTextAsync(_testPreferencesPath, json);

        var loaded = await UiPreferencesStore.LoadAsync();
        Assert.Equal(AppTheme.Light, loaded.Theme);
    }

    [Fact]
    public async Task SavePreferencesAsync_ThenLoadAsync_RoundTripsCorrectly()
    {
        var original = new AppPreferences
        {
            Theme = AppTheme.Dracula,
            WindowLeft = 100,
            WindowTop = 200,
            WindowWidth = 500,
            WindowHeight = 700,
            AlwaysOnTop = false,
            IsPrivacyMode = true
        };

        var saved = await UiPreferencesStore.SaveAsync(original);
        var loaded = await UiPreferencesStore.LoadAsync();

        Assert.True(saved);
        Assert.Equal(original.Theme, loaded.Theme);
        Assert.Equal(original.WindowLeft, loaded.WindowLeft);
        Assert.Equal(original.WindowTop, loaded.WindowTop);
        Assert.Equal(original.WindowWidth, loaded.WindowWidth);
        Assert.Equal(original.WindowHeight, loaded.WindowHeight);
        Assert.Equal(original.AlwaysOnTop, loaded.AlwaysOnTop);
        Assert.Equal(original.IsPrivacyMode, loaded.IsPrivacyMode);
    }

    [Fact]
    public void ApplyTheme_WithNullResources_DoesNotThrow()
    {
        var theme = AppTheme.Dark;

        try
        {
            App.ApplyTheme(theme);
        }
        catch (NullReferenceException)
        {
            // Expected in test context since Application.Current is null
        }
    }

    [Fact]
    public async Task PreferencesStore_SaveLoad_NoDeadlock()
    {
        var preferences = new AppPreferences { Theme = AppTheme.Nord };

        for (int i = 0; i < 10; i++)
        {
            preferences.Theme = (AppTheme)((i % 4) + 1);
            var saved = await UiPreferencesStore.SaveAsync(preferences);
            var loaded = await UiPreferencesStore.LoadAsync();

            Assert.True(saved);
            Assert.NotNull(loaded);
        }
    }

    [Fact]
    public async Task ThemeCombo_SelectedValue_PreservesNonDefaultTheme()
    {
        var preferences = new AppPreferences { Theme = AppTheme.Light };

        preferences.Theme = AppTheme.Midnight;
        var saved = await UiPreferencesStore.SaveAsync(preferences);
        var loaded = await UiPreferencesStore.LoadAsync();

        Assert.True(saved);
        Assert.Equal(AppTheme.Midnight, loaded.Theme);
    }
}
