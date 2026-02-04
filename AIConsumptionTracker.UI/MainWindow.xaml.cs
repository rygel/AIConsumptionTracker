using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using AIConsumptionTracker.Core.Services;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;
using System.Threading.Tasks; 

namespace AIConsumptionTracker.UI
{
    public partial class MainWindow : Window
    {
        private readonly ProviderManager _providerManager;
        private readonly IConfigLoader _configLoader;
        private AppPreferences _preferences = new();

        public MainWindow(ProviderManager providerManager, IConfigLoader configLoader)
        {
            InitializeComponent();
            _providerManager = providerManager;
            _configLoader = configLoader;
            
            Loaded += async (s, e) => {
                // Position window bottom right (moved from MainWindow_Loaded)
                var desktopWorkingArea = SystemParameters.WorkArea;
                this.Left = desktopWorkingArea.Right - this.Width - 10;
                this.Top = desktopWorkingArea.Bottom - this.Height - 10;

                _preferences = await _configLoader.LoadPreferencesAsync();
                ShowAllToggle.IsChecked = _preferences.ShowAll;
                await RefreshData();
            };

            this.Deactivated += (s, e) => {
                // Only hide if the window is visible and enabled (not showing a modal dialog)
                if (this.IsVisible && this.IsEnabled)
                {
                    this.Hide();
                }
            };
        }

        private async void RefreshData_NoArgs(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                _preferences.ShowAll = ShowAllToggle.IsChecked ?? false;
                await _configLoader.SavePreferencesAsync(_preferences);
                await RefreshData();
            }
        }

        private async Task RefreshData()
        {
            ProvidersList.Children.Clear();
            ProvidersList.Children.Add(new TextBlock 
            { 
                Text = "Refreshing...", 
                Foreground = Brushes.Gray, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });

            var usages = await _providerManager.GetAllUsageAsync();
            
            // Update Individual Tray Icons
            var configs = await _configLoader.LoadConfigAsync();
            var app = (App)Application.Current;
            app.UpdateProviderTrayIcons(usages, configs);
            
            ProvidersList.Children.Clear();

            bool showAll = ShowAllToggle?.IsChecked ?? true;
            var filteredUsages = usages.Where(u => showAll || (u.IsAvailable && !u.Description.Contains("not found", StringComparison.OrdinalIgnoreCase))).ToList();

            if (!filteredUsages.Any())
            {
                ProvidersList.Children.Add(new TextBlock 
                { 
                    Text = showAll ? "No providers found." : "No active providers. Toggle 'Show All' to see more.", 
                    Foreground = Brushes.Gray, 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    Margin = new Thickness(0,20,0,0),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                return;
            }

            foreach (var usage in filteredUsages)
            {
                bool isMissing = usage.Description.Contains("not found", StringComparison.OrdinalIgnoreCase);
                bool isConsoleCheck = usage.Description.Contains("Check Console", StringComparison.OrdinalIgnoreCase);
                bool isError = usage.Description.Contains("[Error]", StringComparison.OrdinalIgnoreCase);

                // Main Container
                var container = new Border 
                { 
                    Background = new SolidColorBrush(Color.FromRgb(35,35,35)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0,0,0,12),
                    BorderBrush = isMissing || isError ? Brushes.Maroon : (isConsoleCheck ? Brushes.DarkOrange : new SolidColorBrush(Color.FromRgb(50,50,50))),
                    BorderThickness = new Thickness(1),
                    Opacity = (isMissing || !usage.IsAvailable) ? 0.6 : 1.0
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Name & Account
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bar & Usage Detail
                
                // Header Row (Row 0): [Icon] Name [Account]
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Label (Missing/Console etc)

                var icon = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/usage_icon.png")),
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.8
                };
                headerGrid.Children.Add(icon);

                var accountPart = string.IsNullOrWhiteSpace(usage.AccountName) ? "" : $" [{usage.AccountName}]";
                var nameBlock = new TextBlock 
                { 
                    Text = $"{usage.ProviderName}{accountPart}", 
                    FontWeight = FontWeights.SemiBold, 
                    FontSize = 13,
                    Foreground = isMissing ? Brushes.Gray : Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameBlock, 1);
                headerGrid.Children.Add(nameBlock);
                
                if (isMissing || isConsoleCheck || isError)
                {
                    var statusText = isMissing ? "API Key not found" : (isConsoleCheck ? "Check Console" : "[Error]");
                    var statusBrush = isMissing ? Brushes.IndianRed : (isConsoleCheck ? Brushes.Orange : Brushes.Red);
                    var statusBlock = new TextBlock { Text = statusText, Foreground = statusBrush, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
                    Grid.SetColumn(statusBlock, 2);
                    headerGrid.Children.Add(statusBlock);
                }
                
                grid.Children.Add(headerGrid);

                // Progress & Details Row (Row 1)
                var usageDetailGrid = new Grid { Margin = new Thickness(0,6,0,0) };
                usageDetailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Progress Bar
                usageDetailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Values/Text
                
                // Progress Bar
                if ((usage.UsagePercentage > 0 || usage.IsQuotaBased) && !isMissing && !isError)
                {
                    var pGrid = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) };
                    pGrid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(50,50,50)), CornerRadius = new CornerRadius(2) });
                    
                    var indicatorWidth = Math.Min(usage.UsagePercentage, 100);
                    var fillGrid = new Grid();
                    fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(indicatorWidth, GridUnitType.Star) });
                    fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, 100 - indicatorWidth), GridUnitType.Star) });
                    
                    var fill = new Border 
                    { 
                        Background = usage.UsagePercentage > 90 ? Brushes.Crimson : (usage.UsagePercentage > 75 ? Brushes.Orange : Brushes.MediumSeaGreen),
                        CornerRadius = new CornerRadius(2) 
                    };
                    fillGrid.Children.Add(fill);
                    pGrid.Children.Add(fillGrid);
                    
                    usageDetailGrid.Children.Add(pGrid);
                }

                // Details Text (The tokens/credits/cost)
                var detailText = usage.Description;
                if (!string.IsNullOrEmpty(detailText))
                {
                    var detailBlock = new TextBlock 
                    { 
                        Text = detailText, 
                        FontSize = 11, 
                        Foreground = Brushes.Gray,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(detailBlock, 1);
                    usageDetailGrid.Children.Add(detailBlock);
                }

                grid.Children.Add(usageDetailGrid);
                Grid.SetRow(usageDetailGrid, 1);


                container.Child = grid;
                ProvidersList.Children.Add(container);
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = ((App)Application.Current).Services.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                await RefreshData();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
