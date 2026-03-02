using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Core.Services;
using AIUsageTracker.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.UI.Slim;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly MonitorService _monitorService;
    private readonly MonitorLauncher _monitorLauncher;
    private readonly DispatcherTimer _refreshTimer;
    private AppPreferences _preferences = new();
    private bool _preferencesLoaded = false;
    private List<ProviderUsage> _usages = new();
    private bool _isRefreshing = false;
    private bool _isTooltipOpen = false;
    private bool _isPrivacyMode = false;

    // Test Hooks
    internal Func<Window>? InfoDialogFactory { get; set; }
    internal Func<(Window Window, Func<bool> ShowDialog)> SettingsDialogFactory { get; set; }
    internal Func<Window, bool> ShowOwnedDialog { get; set; }

    public MainWindow() : this(false) { }

    public MainWindow(bool skipUiInitialization)
    {
        InitializeComponent();
        _logger = App.CreateLogger<MainWindow>();

        // Initialize Test Hooks
        SettingsDialogFactory = () => {
            var win = new SettingsWindow();
            return (win, () => win.ShowDialog() == true);
        };
        ShowOwnedDialog = dialog => {
            dialog.Owner = this;
            return dialog.ShowDialog() == true;
        };

        if (skipUiInitialization) return;

        // Load preferences early
        _preferences = UiPreferencesStore.LoadAsync().GetAwaiter().GetResult() ?? new AppPreferences();
        _preferencesLoaded = true;
        _isPrivacyMode = _preferences.IsPrivacyMode;
        App.SetPrivacyMode(_isPrivacyMode);

        // Apply visual preferences
        ApplyVisualPreferences();

        // Initialize Services
        var httpClient = new HttpClient();
        _monitorService = new MonitorService(httpClient, App.CreateLogger<MonitorService>());
        _monitorLauncher = new MonitorLauncher();
        MonitorLauncher.SetLogger(App.CreateLogger<MonitorLauncher>());

        // Setup Timers
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();

        // Register for app-level privacy changes
        App.PrivacyChanged += (s, isPrivate) =>
        {
            _isPrivacyMode = isPrivate;
            _preferences.IsPrivacyMode = isPrivate;
            RenderProviders();
        };

        Loaded += MainWindow_Loaded;
        Deactivated += MainWindow_Deactivated;
    }

    private void ApplyVisualPreferences()
    {
        this.Topmost = _preferences.AlwaysOnTop;
        this.Opacity = _preferences.Opacity;

        if (_preferences.WindowWidth > 0) this.Width = _preferences.WindowWidth;
        if (_preferences.WindowHeight > 0) this.Height = _preferences.WindowHeight;
        if (_preferences.WindowLeft != null) this.Left = _preferences.WindowLeft.Value;
        if (_preferences.WindowTop != null) this.Top = _preferences.WindowTop.Value;

        // Ensure window is on screen
        EnsureWindowIsVisible();

        ShowUsedToggle.IsChecked = _preferences.ShowUsedPercentage;
        AlwaysOnTopCheck.IsChecked = _preferences.AlwaysOnTop;
    }

    private void EnsureWindowIsVisible()
    {
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;

        if (this.Left < 0) this.Left = 0;
        if (this.Top < 0) this.Top = 0;
        if (this.Left + this.Width > screenWidth) this.Left = screenWidth - this.Width;
        if (this.Top + this.Height > screenHeight) this.Top = screenHeight - this.Height;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LogDiagnostic("[DIAGNOSTIC] MainWindow Loaded");

            // Initial load
            await RefreshDataAsync();
            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MainWindow_Loaded");
        }
    }

    private void MainWindow_Deactivated(object sender, EventArgs e)
    {
        if (_preferences.AlwaysOnTop)
        {
            // Reset topmost to ensure it stays above other windows
            this.Topmost = false;
            this.Topmost = true;
        }
    }

    private async Task RefreshDataAsync()
    {
        if (_isRefreshing) return;

        try
        {
            _isRefreshing = true;
            RefreshBtn.IsEnabled = false;
            StatusText.Text = "Refreshing...";

            // Ensure Monitor is running
            var isRunning = await MonitorLauncher.StartAgentAsync();
            if (!isRunning)
            {
                StatusText.Text = "Monitor offline";
                StatusLed.Fill = Brushes.Gray;
                return;
            }

            // Fetch Usage
            var usages = await _monitorService.GetUsageAsync();
            _usages = usages.ToList();

            RenderProviders();

            StatusText.Text = $"{DateTime.Now:HH:mm:ss}";
            StatusLed.Fill = Brushes.LimeGreen;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh data");
            StatusText.Text = "Error";
            StatusLed.Fill = Brushes.Red;
        }
        finally
        {
            _isRefreshing = false;
            RefreshBtn.IsEnabled = true;
        }
    }

    private void LogDiagnostic(string message)
    {
        _logger.LogDebug(message);
        Debug.WriteLine(message);
    }

    private UIElement CreateInfoTextBlock(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 5, 10, 5),
            Foreground = GetResourceBrush("SecondaryText", Brushes.Gray),
            FontStyle = FontStyles.Italic,
            FontSize = 11
        };
    }

    private void RenderProviders()
    {
        LogDiagnostic("[DIAGNOSTIC] RenderProviders called");
        ProvidersList.Children.Clear();

        if (!_usages.Any())
        {
            ProvidersList.Children.Add(CreateInfoTextBlock("No provider data available."));
            ApplyProviderListFontPreferences();
            return;
        }

        try
        {
            var filteredUsages = _usages.ToList();

            // Guard against duplicate provider entries returned by the Agent.
            filteredUsages = filteredUsages
                .GroupBy(u => u.ProviderId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Separate providers by type and order alphabetically by the name THEY provided
            var quotaProviders = filteredUsages
                .Where(u => u.IsQuotaBased)
                .OrderBy(u => u.GetFriendlyName(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.ProviderId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var paygProviders = filteredUsages
                .Where(u => !u.IsQuotaBased)
                .OrderBy(u => u.GetFriendlyName(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.ProviderId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Plans & Quotas Section
            if (quotaProviders.Any())
            {
                var (plansHeader, plansContainer) = CreateCollapsibleGroupHeader(
                    "Plans & Quotas",
                    Brushes.DeepSkyBlue,
                    "PlansAndQuotas",
                    () => _preferences.IsPlansAndQuotasCollapsed,
                    v => _preferences.IsPlansAndQuotasCollapsed = v);

                ProvidersList.Children.Add(plansHeader);
                ProvidersList.Children.Add(plansContainer);

                if (!_preferences.IsPlansAndQuotasCollapsed)
                {
                    foreach (var usage in quotaProviders)
                    {
                        AddProviderCard(usage, plansContainer);

                        if (usage.Details?.Any() == true)
                        {
                            AddCollapsibleSubProviders(usage, plansContainer);
                        }
                    }
                }
            }

            // Pay As You Go Section
            if (paygProviders.Any())
            {
                var (paygHeader, paygContainer) = CreateCollapsibleGroupHeader(
                    "Pay As You Go",
                    Brushes.MediumSeaGreen,
                    "PayAsYouGo",
                    () => _preferences.IsPayAsYouGoCollapsed,
                    v => _preferences.IsPayAsYouGoCollapsed = v);

                ProvidersList.Children.Add(paygHeader);
                ProvidersList.Children.Add(paygContainer);

                if (!_preferences.IsPayAsYouGoCollapsed)
                {
                    foreach (var usage in paygProviders)
                    {
                        AddProviderCard(usage, paygContainer);
                    }
                }
            }

            ApplyProviderListFontPreferences();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenderProviders failed");
            ProvidersList.Children.Clear();
            ProvidersList.Children.Add(CreateInfoTextBlock("Failed to render provider cards."));
            ApplyProviderListFontPreferences();
        }
    }

    private void ApplyProviderListFontPreferences()
    {
        if (ProvidersList == null) return;
        ApplyFontPreferencesToElement(ProvidersList);
    }

    private void ApplyFontPreferencesToElement(DependencyObject element)
    {
        if (element is TextBlock textBlock)
        {
            if (!string.IsNullOrWhiteSpace(_preferences.FontFamily))
            {
                try { textBlock.FontFamily = new FontFamily(_preferences.FontFamily); } catch { }
            }
            if (_preferences.FontSize > 0) textBlock.FontSize = _preferences.FontSize;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            ApplyFontPreferencesToElement(VisualTreeHelper.GetChild(element, i));
        }
    }

    private (UIElement Header, StackPanel Container) CreateCollapsibleHeader(
        string title, Brush accent, bool isGroupHeader, string? groupKey,
        Func<bool> getCollapsed, Action<bool> setCollapsed)
    {
        var isCollapsed = getCollapsed();
        var container = new StackPanel { Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible };

        var headerGrid = CreateCollapsibleHeaderGrid(new Thickness(isGroupHeader ? 0 : 15, isGroupHeader ? 10 : 2, 0, 5));
        headerGrid.Cursor = Cursors.Hand;

        var arrow = new TextBlock
        {
            Text = isCollapsed ? "▶" : "▼",
            Foreground = accent,
            Width = 15,
            FontSize = isGroupHeader ? 12 : 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrow, 0);
        headerGrid.Children.Add(arrow);

        var titleText = new TextBlock
        {
            Text = title.ToUpper(),
            Foreground = accent,
            FontWeight = isGroupHeader ? FontWeights.Bold : FontWeights.Normal,
            FontSize = isGroupHeader ? 11 : 9,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleText, 1);
        headerGrid.Children.Add(titleText);

        if (isGroupHeader)
        {
            var line = new Border { Height = 1, Background = accent, Opacity = 0.3, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(line, 2);
            headerGrid.Children.Add(line);
        }

        headerGrid.MouseDown += (s, e) =>
        {
            var newCollapsed = !getCollapsed();
            setCollapsed(newCollapsed);
            container.Visibility = newCollapsed ? Visibility.Collapsed : Visibility.Visible;
            arrow.Text = newCollapsed ? "▶" : "▼";
            UiPreferencesStore.SaveAsync(_preferences);
        };

        return (headerGrid, container);
    }

    private static Grid CreateCollapsibleHeaderGrid(Thickness margin)
    {
        var header = new Grid { Margin = margin };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return header;
    }

    private SolidColorBrush GetResourceBrush(string key, SolidColorBrush fallback)
    {
        return FindResource(key) as SolidColorBrush ?? fallback;
    }

    private (UIElement Header, StackPanel Container) CreateCollapsibleGroupHeader(
        string title, Brush accent, string groupKey,
        Func<bool> getCollapsed, Action<bool> setCollapsed)
    {
        return CreateCollapsibleHeader(title, accent, isGroupHeader: true, groupKey, getCollapsed, setCollapsed);
    }

    private (UIElement Header, StackPanel Container) CreateCollapsibleSubHeader(
        string title, Brush accent,
        Func<bool> getCollapsed, Action<bool> setCollapsed)
    {
        return CreateCollapsibleHeader(title, accent, isGroupHeader: false, null, getCollapsed, setCollapsed);
    }

    private void AddProviderCard(ProviderUsage usage, StackPanel container, bool isChild = false)
    {
        var providerId = usage.ProviderId ?? string.Empty;
        var friendlyName = usage.GetFriendlyName();
        var description = usage.Description ?? string.Empty;

        bool isMissing = description.Contains("not found", StringComparison.OrdinalIgnoreCase);
        bool isConsoleCheck = description.Contains("Check Console", StringComparison.OrdinalIgnoreCase);
        bool isError = description.Contains("[Error]", StringComparison.OrdinalIgnoreCase);
        bool isUnknown = description.Contains("unknown", StringComparison.OrdinalIgnoreCase);

        var grid = new Grid
        {
            Margin = new Thickness(isChild ? 20 : 0, 0, 0, 2),
            Height = 24,
            Background = Brushes.Transparent,
            Tag = providerId
        };

        bool shouldHaveProgress = usage.IsAvailable && !isUnknown && !isMissing && !isError;

        var pGrid = new Grid();
        bool showUsed = ShowUsedToggle?.IsChecked ?? false;

        if (TryGetDualWindowUsedPercentages(usage, out var hourlyUsed, out var weeklyUsed))
        {
            pGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            pGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var hourlyRow = CreateProgressLayer(hourlyUsed, showUsed, opacity: 0.55);
            var weeklyRow = CreateProgressLayer(weeklyUsed, showUsed, opacity: 0.35);
            Grid.SetRow(hourlyRow, 0);
            Grid.SetRow(weeklyRow, 1);
            pGrid.Children.Add(hourlyRow);
            pGrid.Children.Add(weeklyRow);
        }
        else
        {
            bool isQuotaType = usage.IsQuotaBased;
            double pctRemaining = isQuotaType ? usage.RequestsPercentage : Math.Max(0, 100 - usage.RequestsPercentage);
            double pctUsed = isQuotaType ? Math.Max(0, 100 - usage.RequestsPercentage) : usage.RequestsPercentage;
            
            var indicatorWidth = showUsed ? pctUsed : pctRemaining;
            indicatorWidth = Math.Max(0, Math.Min(100, indicatorWidth));

            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(indicatorWidth, GridUnitType.Star) });
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, 100 - indicatorWidth), GridUnitType.Star) });

            var fill = new Border { Background = GetProgressBarColor(pctUsed), Opacity = 0.45, CornerRadius = new CornerRadius(0) };
            pGrid.Children.Add(fill);
        }

        pGrid.Visibility = shouldHaveProgress ? Visibility.Visible : Visibility.Collapsed;
        grid.Children.Add(pGrid);

        var bg = new Border { Background = GetResourceBrush("CardBackground", Brushes.DarkGray), CornerRadius = new CornerRadius(0), Visibility = shouldHaveProgress ? Visibility.Collapsed : Visibility.Visible };
        grid.Children.Add(bg);

        var contentPanel = new DockPanel { LastChildFill = false, Margin = new Thickness(6, 0, 6, 0) };

        if (isChild)
        {
            var icon = new Border { Width = 4, Height = 4, Background = GetResourceBrush("SecondaryText", Brushes.Gray), CornerRadius = new CornerRadius(2), Margin = new Thickness(2, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            contentPanel.Children.Add(icon);
            DockPanel.SetDock(icon, Dock.Left);
        }
        else
        {
            var providerIcon = CreateProviderIcon(providerId);
            providerIcon.Width = 14; providerIcon.Height = 14; providerIcon.VerticalAlignment = VerticalAlignment.Center;
            providerIcon.Margin = new Thickness(0, 0, 6, 0);
            contentPanel.Children.Add(providerIcon);
            DockPanel.SetDock(providerIcon, Dock.Left);
        }

        string statusText = "";
        Brush statusBrush = GetResourceBrush("SecondaryText", Brushes.Gray);

        if (isMissing) { statusText = "Key Missing"; statusBrush = Brushes.IndianRed; }
        else if (isError) { statusText = "Error"; statusBrush = Brushes.Red; }
        else if (isConsoleCheck) { statusText = "Check Console"; statusBrush = Brushes.Orange; }
        else { statusText = usage.GetStatusText(showUsed); }

        var relative = usage.GetRelativeResetTime();
        if (!string.IsNullOrEmpty(relative))
        {
            var resetBlock = new TextBlock { Text = $"(Resets: {relative})", FontSize = 10, Foreground = GetResourceBrush("StatusTextWarning", Brushes.Goldenrod), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            DockPanel.SetDock(resetBlock, Dock.Right);
            contentPanel.Children.Add(resetBlock);
        }

        var rightBlock = new TextBlock { Text = statusText, FontSize = 10, Foreground = statusBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        DockPanel.SetDock(rightBlock, Dock.Right);
        contentPanel.Children.Add(rightBlock);

        var accountPart = string.IsNullOrWhiteSpace(usage.AccountName) ? "" : $" [{(_isPrivacyMode ? MaskAccountIdentifier(usage.AccountName) : usage.AccountName)}]";
        var nameBlock = new TextBlock { Text = $"{friendlyName}{accountPart}", FontWeight = isChild ? FontWeights.Normal : FontWeights.SemiBold, FontSize = 11, Foreground = isMissing ? GetResourceBrush("TertiaryText", Brushes.Gray) : GetResourceBrush("PrimaryText", Brushes.White), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        contentPanel.Children.Add(nameBlock);
        DockPanel.SetDock(nameBlock, Dock.Left);

        grid.Children.Add(contentPanel);

        if (usage.Details != null && usage.Details.Any())
        {
            var tooltipBuilder = new System.Text.StringBuilder();
            tooltipBuilder.AppendLine($"{friendlyName}");
            if (!string.IsNullOrEmpty(usage.Description)) tooltipBuilder.AppendLine($"Description: {usage.Description}");
            tooltipBuilder.AppendLine("\nRate Limits:");
            foreach (var detail in usage.Details.OrderBy(GetDetailDisplayName, StringComparer.OrdinalIgnoreCase))
            {
                tooltipBuilder.AppendLine($"  {GetDetailDisplayName(detail)}: {detail.Used}");
            }
            
            var toolTip = new ToolTip { Content = tooltipBuilder.ToString().Trim(), Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint, PlacementTarget = grid };
            toolTip.Opened += (s, e) => { _isTooltipOpen = true; if (this.Topmost) { var w = Window.GetWindow(toolTip); if (w != null) w.Topmost = true; } };
            toolTip.Closed += (s, e) => _isTooltipOpen = false;
            grid.ToolTip = toolTip;
        }

        container.Children.Add(grid);
    }

    private void AddCollapsibleSubProviders(ProviderUsage usage, StackPanel container)
    {
        if (usage.Details == null || !usage.Details.Any()) return;

        var displayableDetails = usage.Details
            .Where(d => d.IsDisplayableSubProviderDetail())
            .OrderBy(GetDetailDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!displayableDetails.Any()) return;

        var (subHeader, subContainer) = CreateCollapsibleSubHeader(
            $"{usage.ProviderName} Details",
            Brushes.DeepSkyBlue,
            () => usage.ProviderId == "antigravity" && _preferences.IsAntigravityCollapsed,
            v => { if (usage.ProviderId == "antigravity") _preferences.IsAntigravityCollapsed = v; });

        container.Children.Add(subHeader);
        container.Children.Add(subContainer);

        foreach (var detail in displayableDetails)
        {
            var detailUsage = new ProviderUsage
            {
                ProviderId = $"{usage.ProviderId}.{detail.Name.ToLowerInvariant().Replace(" ", "-")}",
                ProviderName = detail.Name,
                Description = detail.Used,
                IsAvailable = usage.IsAvailable,
                IsQuotaBased = usage.IsQuotaBased,
                PlanType = usage.PlanType
            };
            AddProviderCard(detailUsage, subContainer, isChild: true);
        }
    }

    private static bool TryGetDualWindowUsedPercentages(ProviderUsage usage, out double hourlyUsed, out double weeklyUsed)
    {
        hourlyUsed = weeklyUsed = 0;
        if (usage.Details == null) return false;
        var h = usage.Details.FirstOrDefault(d => d.DetailType == ProviderUsageDetailType.QuotaWindow && d.WindowKind == WindowKind.Primary);
        var w = usage.Details.FirstOrDefault(d => d.DetailType == ProviderUsageDetailType.QuotaWindow && d.WindowKind == WindowKind.Secondary);
        if (h == null || w == null) return false;
        var ph = ParseUsedPercentFromDetail(h.Used);
        var pw = ParseUsedPercentFromDetail(w.Used);
        if (!ph.HasValue || !pw.HasValue) return false;
        hourlyUsed = ph.Value; weeklyUsed = pw.Value;
        return true;
    }

    private static double? ParseUsedPercentFromDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(value, @"(\d+(\.\d+)?)% used");
        if (m.Success && double.TryParse(m.Groups[1].Value, out var used)) return used;
        m = System.Text.RegularExpressions.Regex.Match(value, @"(\d+(\.\d+)?)% remaining");
        if (m.Success && double.TryParse(m.Groups[1].Value, out var rem)) return 100.0 - rem;
        return null;
    }

    private Border CreateProgressLayer(double usedPercent, bool showUsed, double opacity)
    {
        var layerGrid = new Grid();
        double displayWidth = showUsed ? usedPercent : Math.Max(0, 100 - usedPercent);
        displayWidth = Math.Max(0, Math.Min(100, displayWidth));
        layerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(displayWidth, GridUnitType.Star) });
        layerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, 100 - displayWidth), GridUnitType.Star) });
        var fill = new Border { Background = GetProgressBarColor(usedPercent), Opacity = opacity, CornerRadius = new CornerRadius(0) };
        layerGrid.Children.Add(fill);
        return fill;
    }

    private SolidColorBrush GetProgressBarColor(double usedPercent)
    {
        if (usedPercent >= 90) return GetResourceBrush("ProgressBarRed", Brushes.Red);
        if (usedPercent >= 70) return GetResourceBrush("ProgressBarYellow", Brushes.Orange);
        return GetResourceBrush("ProgressBarGreen", Brushes.LimeGreen);
    }

    private FrameworkElement CreateProviderIcon(string providerId)
    {
        var logo = GetProviderLogo(providerId);
        if (logo != null) return new Image { Source = logo, Stretch = Stretch.Uniform };
        var (color, initial) = GetFallbackIconData(providerId);
        return new Border { Background = color, Width = 14, Height = 14, CornerRadius = new CornerRadius(2), Child = new TextBlock { Text = initial, Foreground = Brushes.White, FontSize = 8, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    }

    private DrawingImage? GetProviderLogo(string providerId)
    {
        try
        {
            var normalizedId = providerId.ToLower();
            string filename = normalizedId;
            if (ProviderMetadataCatalog.TryGet(normalizedId, out var definition) && !string.IsNullOrEmpty(definition.LogoKey))
                filename = definition.LogoKey.ToLower();
            else
                filename = filename switch { "github-copilot" => "github", "gemini-cli" => "google", "antigravity" => "google", "claude-code" or "claude" => "anthropic", "minimax" or "minimax-io" or "minimax-global" => "minimax", "kimi" => "kimi", "xiaomi" => "xiaomi", "zai" => "zai", "deepseek" => "deepseek", "openrouter" => "openai", "codex" => "openai", "mistral" => "mistral", "openai" => "openai", "anthropic" => "anthropic", "google" => "google", "github" => "github", _ => filename };

            var svgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ProviderLogos", $"{filename}.svg");
            if (System.IO.File.Exists(svgPath))
            {
                try
                {
                    var settings = new SharpVectors.Renderers.Wpf.WpfDrawingSettings
                    {
                        IncludeRuntime = true,
                        TextAsGeometry = true
                    };
                    var reader = new SharpVectors.Converters.FileSvgReader(settings);
                    var drawing = reader.Read(svgPath);
                    if (drawing != null)
                    {
                        var image = new DrawingImage(drawing);
                        image.Freeze();
                        return image;
                    }
                }
                catch { }
            }
            return null;
        }
        catch { return null; }
    }

    private (Brush Color, string Initial) GetFallbackIconData(string providerId)
    {
        return providerId.ToLower() switch { "antigravity" or "google" or "gemini" => (Brushes.RoyalBlue, "G"), "openai" or "codex" => (Brushes.DarkCyan, "AI"), "anthropic" or "claude" => (Brushes.IndianRed, "An"), "github-copilot" or "github" => (Brushes.MediumPurple, "GH"), "mistral" => (Brushes.Orange, "M"), "deepseek" => (Brushes.DeepSkyBlue, "DS"), "kimi" => (Brushes.Teal, "K"), "zai" => (Brushes.SlateBlue, "Z"), _ => (Brushes.Gray, "?") };
    }

    private static string GetDetailDisplayName(ProviderUsageDetail detail) => detail.Name;

    private static string MaskAccountIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var atIndex = name.IndexOf('@');
        if (atIndex > 0)
        {
            var local = name[..atIndex]; var domain = name[(atIndex + 1)..]; var maskedDomainChars = domain.ToCharArray();
            for (var i = 0; i < maskedDomainChars.Length; i++) { if (maskedDomainChars[i] != '.' && i > 0 && i < maskedDomainChars.Length - 1) maskedDomainChars[i] = '*'; }
            return $"{local[0]}***@{new string(maskedDomainChars)}";
        }
        if (name.Length <= 3) return name[0] + "***";
        return name[0] + "***" + name[^1];
    }

    private static string GetAntigravityModelDisplayName(ProviderUsageDetail detail) => detail.Name;

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = OpenSettingsDialogAsync();
    }

    internal async Task OpenSettingsDialogAsync()
    {
        var (win, show) = SettingsDialogFactory();
        if (show())
        {
            _preferences = await UiPreferencesStore.LoadAsync() ?? new AppPreferences();
            _isPrivacyMode = _preferences.IsPrivacyMode;
            App.SetPrivacyMode(_isPrivacyMode);
            ApplyVisualPreferences();
            _ = RefreshDataAsync();
        }
    }

    private void InfoBtn_Click(object sender, RoutedEventArgs e) { var infoDialog = new InfoDialog(); infoDialog.Owner = this; infoDialog.ShowDialog(); }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await RefreshDataAsync();

    private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5) RefreshBtn_Click(this, new RoutedEventArgs());
        else if (e.Key == Key.F2) SettingsBtn_Click(this, new RoutedEventArgs());
    }

    public void ShowAndActivate()
    {
        this.Show();
        if (this.WindowState == WindowState.Minimized)
        {
            this.WindowState = WindowState.Normal;
        }
        this.Activate();
        this.Focus();
    }

    public async Task PrepareForHeadlessScreenshotAsync(bool deterministic = false)
    {
        _usages = new List<ProviderUsage>
        {
            new ProviderUsage { ProviderId = "openai.primary", ProviderName = "Codex [OpenAI Codex]", RequestsPercentage = 75, IsAvailable = true, IsQuotaBased = true, PlanType = PlanType.Coding, AccountName = "user@example.com" },
            new ProviderUsage { ProviderId = "openai.spark", ProviderName = "GPT-5.3-Codex-Spark [OpenAI Codex]", RequestsPercentage = 40, IsAvailable = true, IsQuotaBased = true, PlanType = PlanType.Coding, AccountName = "user@example.com" },
            new ProviderUsage { ProviderId = "github-copilot", ProviderName = "GitHub Copilot", RequestsPercentage = 95, IsAvailable = true, IsQuotaBased = true, PlanType = PlanType.Coding, AccountName = "dev-user" }
        };

        if (deterministic)
        {
            _isPrivacyMode = true;
            _preferences.IsPrivacyMode = true;
        }

        RenderProviders();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
    {
        if (!_preferencesLoaded || AlwaysOnTopCheck == null) return;
        _preferences.AlwaysOnTop = AlwaysOnTopCheck.IsChecked ?? true;
        this.Topmost = _preferences.AlwaysOnTop;
        UiPreferencesStore.SaveAsync(_preferences);
    }

    private void ShowUsedToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!_preferencesLoaded || ShowUsedToggle == null) return;
        _preferences.ShowUsedPercentage = ShowUsedToggle.IsChecked ?? false;
        RenderProviders();
        UiPreferencesStore.SaveAsync(_preferences);
    }

    private void WebBtn_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("http://localhost:5000") { UseShellExecute = true }); } catch { }
    }

    private async void MonitorToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private void PrivacyBtn_Click(object sender, RoutedEventArgs e)
    {
        _isPrivacyMode = !_isPrivacyMode;
        App.SetPrivacyMode(_isPrivacyMode);
    }

    private void ViewChangelogBtn_Click(object sender, RoutedEventArgs e) { }
    private void UpdateBtn_Click(object sender, RoutedEventArgs e) { }
}
