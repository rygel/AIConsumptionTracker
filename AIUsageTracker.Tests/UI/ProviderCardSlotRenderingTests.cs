// <copyright file="ProviderCardSlotRenderingTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIUsageTracker.Core.Models;
using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

/// <summary>
/// Tests that ProviderCardRenderer slot rendering respects boolean preference toggles.
/// Prevents regressions where slot-based rendering bypasses existing display settings.
/// </summary>
[Collection("WPF")]
public sealed class ProviderCardSlotRenderingTests
{
    private static ProviderCardRenderer CreateRenderer(AppPreferences prefs)
    {
        return new ProviderCardRenderer(
            prefs,
            isPrivacyMode: false,
            (_, fallback) => fallback,
            _ => new Border { Width = 14, Height = 14 },
            (_, _) => new ToolTip(),
            _ => { },
            UsageMath.FormatRelativeTime);
    }

    private static ProviderUsage CreateUsage(double usedPercent = 50, double? usagePerHour = 12.5, DateTime? nextResetTime = null, TimeSpan? periodDuration = null)
    {
        return new ProviderUsage
        {
            ProviderId = "test",
            ProviderName = "Test Provider",
            UsedPercent = usedPercent,
            IsAvailable = true,
            IsQuotaBased = true,
            PlanType = PlanType.Usage,
            Description = $"{100 - usedPercent:F0}% Remaining",
            UsagePerHour = usagePerHour,
            NextResetTime = nextResetTime ?? DateTime.UtcNow.AddDays(3),
            PeriodDuration = periodDuration ?? TimeSpan.FromDays(7),
        };
    }

    private static string GetAllTextFromVisual(FrameworkElement element)
    {
        var texts = new List<string>();
        CollectTexts(element, texts);
        return string.Join(" | ", texts);
    }

    private static void CollectTexts(DependencyObject obj, List<string> texts)
    {
        if (obj is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
        {
            texts.Add(tb.Text);
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            CollectTexts(VisualTreeHelper.GetChild(obj, i), texts);
        }

        if (obj is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                CollectTexts(child, texts);
            }
        }

        if (obj is Decorator decorator && decorator.Child != null)
        {
            CollectTexts(decorator.Child, texts);
        }
    }

    [StaFact]
    public void UsageRate_ShownWhenEnabled()
    {
        var prefs = new AppPreferences
        {
            ShowUsagePerHour = true,
            CardSecondaryBadge = CardSlotContent.UsageRate,
        };
        var renderer = CreateRenderer(prefs);
        var card = renderer.CreateProviderCard(CreateUsage(usagePerHour: 12.5), showUsed: false);
        var text = GetAllTextFromVisual(card);

        Assert.Contains("/hr", text);
    }

    [StaFact]
    public void UsageRate_HiddenWhenDisabled()
    {
        var prefs = new AppPreferences
        {
            ShowUsagePerHour = false,
            CardSecondaryBadge = CardSlotContent.UsageRate,
        };
        var renderer = CreateRenderer(prefs);
        var card = renderer.CreateProviderCard(CreateUsage(usagePerHour: 12.5), showUsed: false);
        var text = GetAllTextFromVisual(card);

        Assert.DoesNotContain("/hr", text);
    }

    [StaFact]
    public void PaceBadge_ShownWhenPaceAdjustmentEnabled()
    {
        var prefs = new AppPreferences
        {
            EnablePaceAdjustment = true,
            CardPrimaryBadge = CardSlotContent.PaceBadge,
        };
        var renderer = CreateRenderer(prefs);
        var usage = CreateUsage(usedPercent: 30);
        var card = renderer.CreateProviderCard(usage, showUsed: false);
        var text = GetAllTextFromVisual(card);

        // Should show one of: Headroom, On pace, Over pace
        Assert.True(
            text.Contains("Headroom") || text.Contains("On pace") || text.Contains("Over pace"),
            $"Expected pace badge text, got: {text}");
    }

    [StaFact]
    public void PaceBadge_HiddenWhenPaceAdjustmentDisabled()
    {
        var prefs = new AppPreferences
        {
            EnablePaceAdjustment = false,
            CardPrimaryBadge = CardSlotContent.PaceBadge,
        };
        var renderer = CreateRenderer(prefs);
        var usage = CreateUsage(usedPercent: 30);
        var card = renderer.CreateProviderCard(usage, showUsed: false);
        var text = GetAllTextFromVisual(card);

        Assert.DoesNotContain("Headroom", text);
        Assert.DoesNotContain("On pace", text);
        Assert.DoesNotContain("Over pace", text);
    }

    [StaFact]
    public void ResetSlot_RespectsUseRelativeResetTimePreference()
    {
        var prefs = new AppPreferences
        {
            UseRelativeResetTime = true,
            CardResetInfo = CardSlotContent.ResetAbsolute,
        };
        var renderer = CreateRenderer(prefs);
        var usage = CreateUsage();
        var card = renderer.CreateProviderCard(usage, showUsed: false);
        var text = GetAllTextFromVisual(card);

        // When UseRelativeResetTime is true and slot is ResetAbsolute,
        // the format should switch to relative (e.g. "2d 23h")
        Assert.Matches(@"\d+[dhm]", text);
    }

    [StaFact]
    public void StatusText_AlwaysShown()
    {
        var prefs = new AppPreferences
        {
            CardStatusLine = CardSlotContent.StatusText,
        };
        var renderer = CreateRenderer(prefs);
        var usage = CreateUsage(usedPercent: 40);
        var card = renderer.CreateProviderCard(usage, showUsed: false);
        var text = GetAllTextFromVisual(card);

        Assert.Contains("Remaining", text);
    }

    [StaFact]
    public void SlotSetToNone_RendersNothing()
    {
        var prefs = new AppPreferences
        {
            CardPrimaryBadge = CardSlotContent.None,
            CardSecondaryBadge = CardSlotContent.None,
            CardStatusLine = CardSlotContent.None,
            CardResetInfo = CardSlotContent.None,
            ShowUsagePerHour = true,
            EnablePaceAdjustment = true,
        };
        var renderer = CreateRenderer(prefs);
        var usage = CreateUsage(usedPercent: 30, usagePerHour: 10.0);
        var card = renderer.CreateProviderCard(usage, showUsed: false);
        var text = GetAllTextFromVisual(card);

        // Only provider name should remain
        Assert.DoesNotContain("/hr", text);
        Assert.DoesNotContain("Headroom", text);
        Assert.DoesNotContain("On pace", text);
        Assert.DoesNotContain("Remaining", text);
    }

    [StaFact]
    public void CardSlotPreferences_DefaultsMatchLegacyBehavior()
    {
        // Default preferences should produce the same card layout as the old hardcoded renderer:
        // PaceBadge + UsageRate + StatusText + ResetAbsolute
        var prefs = new AppPreferences();

        Assert.Equal(CardSlotContent.PaceBadge, prefs.CardPrimaryBadge);
        Assert.Equal(CardSlotContent.UsageRate, prefs.CardSecondaryBadge);
        Assert.Equal(CardSlotContent.StatusText, prefs.CardStatusLine);
        Assert.Equal(CardSlotContent.ResetAbsolute, prefs.CardResetInfo);
    }
}
