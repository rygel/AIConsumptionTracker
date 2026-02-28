using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using AIUsageTracker.Core.Models;
using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

/// <summary>
/// Tests that verify providers actually appear in the UI.
/// </summary>
public class ProviderDisplayIntegrationTests
{
    /// <summary>
    /// This test catches the bug where UI loads but shows no providers.
    /// </summary>
    [Fact]
    public Task UI_ShouldDisplay_WhenMonitorReturnsData()
    {
        return RunInStaAsync(async () =>
        {
            EnsureAppCreated();

            var mainWindow = new MainWindow(skipUiInitialization: false);
            var log = new List<string>();
            
            // Hook into loaded event
            mainWindow.Loaded += (s, e) => log.Add($"[{DateTime.Now:HH:mm:ss.fff}] Loaded");
            
            mainWindow.Show();
            
            // Wait for initialization
            await Task.Delay(TimeSpan.FromSeconds(10));
            
            // Check providers list
            var providersList = FindChildByName(mainWindow, "ProvidersList") as System.Windows.Controls.StackPanel;
            
            Assert.NotNull(providersList);
            log.Add($"[{DateTime.Now:HH:mm:ss.fff}] ProvidersList found, children: {providersList.Children.Count}");
            
            // The UI should show SOMETHING - either providers or a message
            Assert.True(providersList.Children.Count > 0, 
                $"ProvidersList should have content. Log:\n{string.Join("\n", log)}");
            
            mainWindow.Close();
        });
    }

    private static DependencyObject FindChildByName(DependencyObject parent, string name)
    {
        if (parent == null) return null;
        
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name)
                return child;
            
            var result = FindChildByName(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static App EnsureAppCreated()
    {
        if (Application.Current == null)
        {
            var app = new App();
            app.InitializeComponent();
            return app;
        }
        return (App)Application.Current;
    }

    private static Task RunInStaAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object>();
        var thread = new Thread(() =>
        {
            try
            {
                action().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.SetException(t.Exception.InnerExceptions);
                    else if (t.IsCanceled)
                        tcs.SetCanceled();
                    else
                        tcs.SetResult(null);
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
