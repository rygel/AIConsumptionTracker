using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;
using AIConsumptionTracker.Core.Services;
using AIConsumptionTracker.Infrastructure.Configuration;
using AIConsumptionTracker.Infrastructure.Providers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AIConsumptionTracker.UI
{
    public partial class App : Application
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly Dictionary<string, TaskbarIcon> _providerTrayIcons = new();
        private IHost? _host;
        public IServiceProvider Services => _host!.Services;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create Icon (Lazy way: we might want a simple icon resource)
            // Ideally we need an .ico file. For now, we might crash if we don't have one? 
            // Hardcodet requires an IconSource.
            
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => 
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient(); // Required for providers
                    services.AddSingleton<IConfigLoader, JsonConfigLoader>();
                    
                    // Register Providers
                    services.AddTransient<IProviderService, SimulatedProvider>(); 
                    services.AddTransient<IProviderService, OpenCodeProvider>();
                    services.AddTransient<IProviderService, ZaiProvider>();
                    services.AddTransient<IProviderService, OpenRouterProvider>();
                    services.AddTransient<IProviderService, AntigravityProvider>();
                    services.AddTransient<IProviderService, GeminiProvider>();
                    services.AddTransient<IProviderService, OpenCodeZenProvider>();
                    services.AddTransient<IProviderService, GenericPayAsYouGoProvider>();
                    
                    services.AddSingleton<ProviderManager>();
                    services.AddSingleton<MainWindow>(); // Dashboard
                    services.AddTransient<SettingsWindow>();
                })
                .Build();

            await _host.StartAsync();

            InitializeTrayIcon();
            ShowDashboard();
        }

        private void InitializeTrayIcon()
        {
            _taskbarIcon = new TaskbarIcon();
            _taskbarIcon.ToolTipText = "AI Consumption Tracker";
            _taskbarIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/app_icon.png"));

            // Context Menu
            var contextMenu = new ContextMenu();
            
            var openItem = new MenuItem { Header = "Open" };
            openItem.Click += (s, e) => ShowDashboard();
            
            var settingsItem = new MenuItem { Header = "Settings" };
            settingsItem.Click += (s, e) => ShowSettings();
            
            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => ExitApp();

            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);

            _taskbarIcon.ContextMenu = contextMenu;

            // Wire up single click and double click to show dashboard
            _taskbarIcon.TrayLeftMouseDown += (s, e) => ShowDashboard();
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowDashboard();
        }

        private void ShowSettings()
        {
            var mainWindow = _host?.Services.GetRequiredService<MainWindow>();
            if (mainWindow == null) return;
            
            if (mainWindow.Visibility != Visibility.Visible)
            {
                mainWindow.Show();
            }
            mainWindow.Activate();

            var settingsWindow = Services.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = mainWindow;
            settingsWindow.ShowDialog();
        }

        private void ExitApp()
        {
            Application.Current.Shutdown();
        }

        private void ShowDashboard()
        {
            var mainWindow = _host?.Services.GetRequiredService<MainWindow>();
            if (mainWindow != null)
            {
                if (mainWindow.Visibility == Visibility.Visible && mainWindow.IsActive)
                {
                   mainWindow.Hide();
                }
                else
                {
                   mainWindow.Show();
                   mainWindow.Activate();
                }
            }
        }

        public void UpdateProviderTrayIcons(List<ProviderUsage> usages, List<ProviderConfig> configs)
        {
            var desiredIcons = new Dictionary<string, (string ToolTip, double Percentage)>();

            foreach (var config in configs)
            {
                var usage = usages.FirstOrDefault(u => u.ProviderId.Equals(config.ProviderId, StringComparison.OrdinalIgnoreCase));
                if (usage == null) continue;

                // Main Tray
                if (config.ShowInTray)
                {
                    desiredIcons[config.ProviderId] = (
                        $"{usage.ProviderName}: {usage.Description}",
                        usage.UsagePercentage
                    );
                }

                // Sub Trays (e.g. Antigravity credits or specific models)
                if (config.EnabledSubTrays != null && usage.Details != null)
                {
                    foreach (var subName in config.EnabledSubTrays)
                    {
                        var detail = usage.Details.FirstOrDefault(d => d.Name.Equals(subName, StringComparison.OrdinalIgnoreCase));
                        if (detail != null)
                        {
                            // Parse percentage from "85%" style string in Used property
                            double pct = 0;
                            var usedStr = detail.Used.TrimEnd('%');
                            if (double.TryParse(usedStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedPct)) 
                            {
                                pct = parsedPct;
                            }

                            var key = $"{config.ProviderId}:{subName}";
                            desiredIcons[key] = (
                                $"{usage.ProviderName} - {subName}: {detail.Description} ({detail.Used})",
                                pct
                            );
                        }
                    }
                }
            }

            // 1. Remove icons no longer in desiredIcons
            var currentKeys = _providerTrayIcons.Keys.ToList();
            foreach (var key in currentKeys)
            {
                if (!desiredIcons.ContainsKey(key))
                {
                    _providerTrayIcons[key].Dispose();
                    _providerTrayIcons.Remove(key);
                }
            }

            // 2. Add or update desired icons
            foreach (var kvp in desiredIcons)
            {
                var key = kvp.Key;
                var info = kvp.Value;

                if (!_providerTrayIcons.ContainsKey(key))
                {
                    var tray = new TaskbarIcon();
                    tray.ToolTipText = info.ToolTip;
                    tray.IconSource = GenerateUsageIcon(info.Percentage);
                    tray.TrayLeftMouseDown += (s, e) => ShowDashboard();
                    tray.TrayMouseDoubleClick += (s, e) => ShowDashboard();
                    _providerTrayIcons.Add(key, tray);
                }
                else
                {
                    var tray = _providerTrayIcons[key];
                    tray.ToolTipText = info.ToolTip;
                    tray.IconSource = GenerateUsageIcon(info.Percentage);
                }
            }
        }

        private System.Windows.Media.ImageSource GenerateUsageIcon(double percentage)
        {
            int size = 32; 
            var visual = new System.Windows.Media.DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Background (Dark)
                dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 20)), null, new Rect(0, 0, size, size));
                
                // Outer Border
                dc.DrawRectangle(null, new System.Windows.Media.Pen(System.Windows.Media.Brushes.DimGray, 1), new Rect(0.5, 0.5, size - 1, size - 1));

                // Fill logic
                var fillBrush = percentage > 85 ? System.Windows.Media.Brushes.Crimson : (percentage > 60 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.MediumSeaGreen);
                
                double barWidth = size - 6;
                double barHeight = size - 6;
                double fillHeight = (percentage / 100.0) * barHeight;

                // Draw Bar
                dc.DrawRectangle(fillBrush, null, new Rect(3, size - 3 - fillHeight, barWidth, fillHeight));
            }

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            _taskbarIcon?.Dispose();
            foreach (var tray in _providerTrayIcons.Values) tray.Dispose();
            _providerTrayIcons.Clear();

            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }
    }
}

