// <copyright file="CardDesignerWindow.xaml.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;
using AIUsageTracker.UI.Slim.ViewModels;

namespace AIUsageTracker.UI.Slim;

public partial class CardDesignerWindow : Window
{
    private readonly AppPreferences _preferences;
    private readonly List<ProviderUsage> _sampleUsages;

    public CardDesignerWindow(AppPreferences preferences, IReadOnlyList<ProviderUsage> usages)
    {
        this.InitializeComponent();
        this._preferences = preferences;
        this._sampleUsages = usages.Take(5).ToList();

        this.PopulateSlotOptions();
        this.ApplyPreset(CardPreset.Detailed);
        this.RenderPreview();
    }

    private enum CardSlotContent
    {
        None,
        PaceBadge,
        ProjectedPercent,
        DailyBudget,
        UsageRate,
        UsedPercent,
        RemainingPercent,
        ResetAbsolute,
        ResetRelative,
        AccountName,
        StatusText,
        AuthSource,
    }

    private enum CardPreset
    {
        Compact,
        Detailed,
        PaceFocus,
    }

    private void PopulateSlotOptions()
    {
        var options = new[]
        {
            new { Value = CardSlotContent.None, Label = "(empty)" },
            new { Value = CardSlotContent.PaceBadge, Label = "Pace badge (On pace / Over pace)" },
            new { Value = CardSlotContent.ProjectedPercent, Label = "Projected % at reset" },
            new { Value = CardSlotContent.DailyBudget, Label = "Daily budget (14%/day)" },
            new { Value = CardSlotContent.UsageRate, Label = "Usage rate (req/hr)" },
            new { Value = CardSlotContent.UsedPercent, Label = "Used %" },
            new { Value = CardSlotContent.RemainingPercent, Label = "Remaining %" },
            new { Value = CardSlotContent.ResetAbsolute, Label = "Reset time (Saturday 17:44)" },
            new { Value = CardSlotContent.ResetRelative, Label = "Reset time (4d 22h)" },
            new { Value = CardSlotContent.AccountName, Label = "Account name" },
            new { Value = CardSlotContent.StatusText, Label = "Status text" },
            new { Value = CardSlotContent.AuthSource, Label = "Auth source" },
        };

        foreach (var combo in new[] { this.PrimaryBadgeSlot, this.SecondaryBadgeSlot, this.StatusLineSlot, this.ResetInfoSlot })
        {
            combo.ItemsSource = options;
            combo.DisplayMemberPath = "Label";
            combo.SelectedValuePath = "Value";
        }
    }

    private void ApplyPreset(CardPreset preset)
    {
        switch (preset)
        {
            case CardPreset.Compact:
                this.PrimaryBadgeSlot.SelectedValue = CardSlotContent.UsedPercent;
                this.SecondaryBadgeSlot.SelectedValue = CardSlotContent.None;
                this.StatusLineSlot.SelectedValue = CardSlotContent.None;
                this.ResetInfoSlot.SelectedValue = CardSlotContent.ResetAbsolute;
                break;

            case CardPreset.Detailed:
                this.PrimaryBadgeSlot.SelectedValue = CardSlotContent.PaceBadge;
                this.SecondaryBadgeSlot.SelectedValue = CardSlotContent.UsageRate;
                this.StatusLineSlot.SelectedValue = CardSlotContent.StatusText;
                this.ResetInfoSlot.SelectedValue = CardSlotContent.ResetAbsolute;
                break;

            case CardPreset.PaceFocus:
                this.PrimaryBadgeSlot.SelectedValue = CardSlotContent.PaceBadge;
                this.SecondaryBadgeSlot.SelectedValue = CardSlotContent.ProjectedPercent;
                this.StatusLineSlot.SelectedValue = CardSlotContent.DailyBudget;
                this.ResetInfoSlot.SelectedValue = CardSlotContent.ResetAbsolute;
                break;
        }
    }

    private void RenderPreview()
    {
        this.PreviewStack.Children.Clear();

        if (this._sampleUsages.Count == 0)
        {
            this.PreviewStack.Children.Add(new TextBlock
            {
                Text = "No provider data available. Start the Monitor first.",
                Foreground = (Brush)this.FindResource("SecondaryText"),
                Margin = new Thickness(10),
            });
            return;
        }

        foreach (var usage in this._sampleUsages)
        {
            var card = this.BuildPreviewCard(usage);
            this.PreviewStack.Children.Add(card);
        }
    }

    private Border BuildPreviewCard(ProviderUsage usage)
    {
        var displayName = ProviderMetadataCatalog.ResolveDisplayLabel(usage);
        var primaryText = this.ResolveSlotText(usage, (CardSlotContent?)this.PrimaryBadgeSlot.SelectedValue ?? CardSlotContent.None);
        var secondaryText = this.ResolveSlotText(usage, (CardSlotContent?)this.SecondaryBadgeSlot.SelectedValue ?? CardSlotContent.None);
        var statusText = this.ResolveSlotText(usage, (CardSlotContent?)this.StatusLineSlot.SelectedValue ?? CardSlotContent.None);
        var resetText = this.ResolveSlotText(usage, (CardSlotContent?)this.ResetInfoSlot.SelectedValue ?? CardSlotContent.None);

        var usedPercent = usage.UsedPercent;
        var barWidth = Math.Max(0, Math.Min(100, usedPercent));

        // Card container
        var card = new Border
        {
            Background = (Brush)this.FindResource("CardBackground"),
            BorderBrush = (Brush)this.FindResource("CardBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 4),
        };

        var stack = new StackPanel();

        // Row 1: Name + badges
        var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

        if (!string.IsNullOrWhiteSpace(resetText))
        {
            var resetBlock = new TextBlock
            {
                Text = resetText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)this.FindResource("StatusTextWarning"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(resetBlock, Dock.Right);
            topRow.Children.Add(resetBlock);
        }

        if (!string.IsNullOrWhiteSpace(primaryText))
        {
            var primaryBlock = new TextBlock
            {
                Text = primaryText,
                FontSize = 9,
                Foreground = GetBadgeColor(primaryText),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(primaryBlock, Dock.Right);
            topRow.Children.Add(primaryBlock);
        }

        if (!string.IsNullOrWhiteSpace(secondaryText))
        {
            var secondaryBlock = new TextBlock
            {
                Text = secondaryText,
                FontSize = 9,
                Foreground = (Brush)this.FindResource("TertiaryText"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(secondaryBlock, Dock.Right);
            topRow.Children.Add(secondaryBlock);
        }

        var nameBlock = new TextBlock
        {
            Text = displayName,
            FontSize = 11,
            Foreground = (Brush)this.FindResource("PrimaryText"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        topRow.Children.Add(nameBlock);
        stack.Children.Add(topRow);

        // Row 2: Progress bar
        if (usage.IsAvailable && usage.IsQuotaBased)
        {
            var barGrid = new Grid { Height = 4, Margin = new Thickness(0, 2, 0, 2) };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(barWidth, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 100 - barWidth), GridUnitType.Star) });

            var barFill = new Border
            {
                Background = GetBarColor(usedPercent),
                Opacity = 0.45,
            };
            Grid.SetColumn(barFill, 0);
            barGrid.Children.Add(barFill);
            stack.Children.Add(barGrid);
        }

        // Row 3: Status line
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            var statusBlock = new TextBlock
            {
                Text = statusText,
                FontSize = 10,
                Foreground = (Brush)this.FindResource("SecondaryText"),
                Margin = new Thickness(0, 2, 0, 0),
            };
            stack.Children.Add(statusBlock);
        }

        card.Child = stack;
        return card;
    }

    private string ResolveSlotText(ProviderUsage usage, CardSlotContent slot)
    {
        return slot switch
        {
            CardSlotContent.None => string.Empty,
            CardSlotContent.PaceBadge => GetPaceBadgeText(usage),
            CardSlotContent.ProjectedPercent => GetProjectedText(usage),
            CardSlotContent.DailyBudget => GetDailyBudgetText(usage),
            CardSlotContent.UsageRate => usage.UsagePerHour.HasValue ? $"{usage.UsagePerHour.Value:F1}/hr" : string.Empty,
            CardSlotContent.UsedPercent => $"{usage.UsedPercent:F0}% used",
            CardSlotContent.RemainingPercent => $"{Math.Max(0, 100 - usage.UsedPercent):F0}% remaining",
            CardSlotContent.ResetAbsolute => GetAbsoluteResetText(usage),
            CardSlotContent.ResetRelative => GetRelativeResetText(usage),
            CardSlotContent.AccountName => usage.AccountName ?? string.Empty,
            CardSlotContent.StatusText => usage.Description ?? string.Empty,
            CardSlotContent.AuthSource => usage.AuthSource ?? string.Empty,
            _ => string.Empty,
        };
    }

    private static string GetPaceBadgeText(ProviderUsage usage)
    {
        if (!usage.PeriodDuration.HasValue || !usage.NextResetTime.HasValue)
        {
            return string.Empty;
        }

        if (usage.NextResetTime.Value.Ticks < usage.PeriodDuration.Value.Ticks)
        {
            return string.Empty;
        }

        var projected = UsageMath.CalculateProjectedFinalPercent(
            usage.UsedPercent,
            usage.NextResetTime.Value.ToUniversalTime(),
            usage.PeriodDuration.Value);

        if (projected >= 100.0)
        {
            return "Over pace";
        }

        return projected < 90.0 ? "On pace" : string.Empty;
    }

    private static string GetProjectedText(ProviderUsage usage)
    {
        if (!usage.PeriodDuration.HasValue || !usage.NextResetTime.HasValue)
        {
            return string.Empty;
        }

        if (usage.NextResetTime.Value.Ticks < usage.PeriodDuration.Value.Ticks)
        {
            return string.Empty;
        }

        var projected = UsageMath.CalculateProjectedFinalPercent(
            usage.UsedPercent,
            usage.NextResetTime.Value.ToUniversalTime(),
            usage.PeriodDuration.Value);

        return $"Projected: {projected:F0}%";
    }

    private static string GetDailyBudgetText(ProviderUsage usage)
    {
        if (!usage.PeriodDuration.HasValue || usage.PeriodDuration.Value.TotalDays < 1)
        {
            return string.Empty;
        }

        var dailyBudget = 100.0 / usage.PeriodDuration.Value.TotalDays;
        return $"{dailyBudget:F0}%/day budget";
    }

    private static string GetAbsoluteResetText(ProviderUsage usage)
    {
        if (!usage.NextResetTime.HasValue)
        {
            return string.Empty;
        }

        var local = usage.NextResetTime.Value.Kind == DateTimeKind.Utc
            ? usage.NextResetTime.Value.ToLocalTime()
            : usage.NextResetTime.Value;
        var diff = local - DateTime.Now;

        if (diff.TotalSeconds <= 0)
        {
            return "now";
        }

        if (local.Date == DateTime.Today)
        {
            return local.ToString("HH:mm");
        }

        if (local.Date == DateTime.Today.AddDays(1))
        {
            return $"Tomorrow {local:HH:mm}";
        }

        if (diff.TotalDays < 7)
        {
            return $"{local:dddd HH:mm}";
        }

        return $"{local:MMM d HH:mm}";
    }

    private static string GetRelativeResetText(ProviderUsage usage)
    {
        if (!usage.NextResetTime.HasValue)
        {
            return string.Empty;
        }

        var diff = usage.NextResetTime.Value - DateTime.Now;
        if (diff.TotalSeconds <= 0)
        {
            return "0m";
        }

        if (diff.TotalDays >= 1)
        {
            return $"{diff.Days}d {diff.Hours}h";
        }

        return diff.TotalHours >= 1 ? $"{diff.Hours}h {diff.Minutes}m" : $"{diff.Minutes}m";
    }

    private Brush GetBadgeColor(string text)
    {
        if (text.Contains("Over pace", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.OrangeRed;
        }

        if (text.Contains("On pace", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.MediumSeaGreen;
        }

        return (Brush)this.FindResource("SecondaryText");
    }

    private Brush GetBarColor(double usedPercent)
    {
        if (usedPercent >= this._preferences.ColorThresholdRed)
        {
            return Brushes.OrangeRed;
        }

        if (usedPercent >= this._preferences.ColorThresholdYellow)
        {
            return Brushes.Gold;
        }

        return Brushes.MediumSeaGreen;
    }

    // --- Event handlers ---

    private void SlotConfig_Changed(object sender, SelectionChangedEventArgs e) => this.RenderPreview();

    private void PresetCompact_Click(object sender, RoutedEventArgs e)
    {
        this.ApplyPreset(CardPreset.Compact);
        this.RenderPreview();
    }

    private void PresetDetailed_Click(object sender, RoutedEventArgs e)
    {
        this.ApplyPreset(CardPreset.Detailed);
        this.RenderPreview();
    }

    private void PresetPace_Click(object sender, RoutedEventArgs e)
    {
        this.ApplyPreset(CardPreset.PaceFocus);
        this.RenderPreview();
    }

    private void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        // TODO: save slot configuration to preferences and apply to MainWindow
        MessageBox.Show("Card layout configuration will be saved to preferences in a future update.",
            "Not yet implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => this.DragMove();

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => this.Close();
}
