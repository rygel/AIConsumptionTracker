// <copyright file="ProviderCardViewModel.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIUsageTracker.UI.Slim.ViewModels;

/// <summary>
/// ViewModel for a provider card, wrapping ProviderUsage with presentation logic.
/// </summary>
public partial class ProviderCardViewModel : BaseViewModel
{
    [ObservableProperty]
    private ProviderUsage _usage;

    [ObservableProperty]
    private bool _isPrivacyMode;

    [ObservableProperty]
    private int _yellowThreshold = 60;

    [ObservableProperty]
    private int _redThreshold = 80;

    [ObservableProperty]
    private bool _showUsedPercentages;

    [ObservableProperty]
    private bool _showUsagePerHour;

    [ObservableProperty]
    private bool _showDualQuotaBars = true;

    [ObservableProperty]
    private DualQuotaSingleBarMode _dualQuotaSingleBarMode = DualQuotaSingleBarMode.Rolling;

    [ObservableProperty]
    private bool _enablePaceAdjustment = true;

    [ObservableProperty]
    private bool _useRelativeResetTime;

    [ObservableProperty]
    private ObservableCollection<SubProviderCardViewModel> _details = new();

    private ProviderCardPresentation? _presentation;

    public ProviderCardViewModel(ProviderUsage usage, AppPreferences prefs, bool isPrivacyMode)
    {
        this._usage = usage;
        this._isPrivacyMode = isPrivacyMode;
        this._yellowThreshold = prefs.ColorThresholdYellow;
        this._redThreshold = prefs.ColorThresholdRed;
        this._showUsedPercentages = prefs.ShowUsedPercentages;
        this._showUsagePerHour = prefs.ShowUsagePerHour;
        this._showDualQuotaBars = prefs.ShowDualQuotaBars;
        this._dualQuotaSingleBarMode = prefs.DualQuotaSingleBarMode;
        this._enablePaceAdjustment = prefs.EnablePaceAdjustment;
        this._useRelativeResetTime = prefs.UseRelativeResetTime;

        this.UpdatePresentation();
        this.PopulateDetails();
    }

    public string ProviderId => this.Usage.ProviderId ?? string.Empty;

    public string DisplayName => ProviderMetadataCatalog.ResolveDisplayLabel(this.Usage);

    public string AccountDisplay => this.IsPrivacyMode
        ? "****"
        : MainWindowRuntimeLogic.ResolveDisplayAccountName(this.ProviderId, this.Usage.AccountName, false);

    public bool HasAccountName => !string.IsNullOrWhiteSpace(this.Usage.AccountName);

    public double ProgressPercentage
    {
        get
        {
            if (this._presentation?.HasDualBuckets == true && !this.ShowDualQuotaBars)
            {
                var used = this.GetSingleBarDualQuotaUsedPercent();
                var remaining = Math.Max(0, 100 - used);
                return this.ShowUsedPercentages ? used : remaining;
            }

            return this.ShowUsedPercentages ? this.UsedPercent : this.RemainingPercent;
        }
    }

    public double UsedPercent => this._presentation?.UsedPercent ?? 0;

    public double RemainingPercent => this._presentation?.RemainingPercent ?? 100;

    public bool ShouldShowProgress => this._presentation?.ShouldHaveProgress ?? false;

    public string StatusText
    {
        get
        {
            var presentation = this._presentation;
            if (presentation?.HasDualBuckets == true && !this.ShowDualQuotaBars)
            {
                return MainWindowRuntimeLogic.BuildSingleDualQuotaStatusText(
                    presentation,
                    this.ShowUsedPercentages,
                    this.DualQuotaSingleBarMode);
            }

            return presentation?.StatusText ?? string.Empty;
        }
    }

    public ProviderCardStatusTone StatusTone => this._presentation?.StatusTone ?? ProviderCardStatusTone.Secondary;

    public bool IsMissing => this._presentation?.IsMissing ?? false;

    public bool IsStale => this._presentation?.IsStale ?? false;

    /// <summary>
    /// Gets a value indicating whether true when the provider API returned HTTP 429 (Too Many Requests).
    /// The card will show a Warning-tone status rather than an Error-tone status.
    /// </summary>
    public bool IsRateLimited => this.Usage.HttpStatus == 429;

    public bool IsQuotaBased => this.Usage.IsQuotaBased;

    public bool HasDualQuotaBuckets => (this._presentation?.HasDualBuckets ?? false) && this.ShowDualQuotaBars;

    public double PrimaryUsedPercent => this._presentation?.DualBucketPrimaryUsed ?? 0;

    public double SecondaryUsedPercent => this._presentation?.DualBucketSecondaryUsed ?? 0;

    /// <summary>
    /// Pace-adjusted color percent for the primary (burst) bar.
    /// Falls back to raw used percent only when pace data is unavailable (no reset time or period).
    /// </summary>
    public double PrimaryColorPercent => this._presentation?.DualBucketPrimaryColorPercent
                                         ?? this.PrimaryUsedPercent;

    /// <summary>
    /// Pace-adjusted color percent for the secondary (weekly) bar.
    /// Falls back to raw used percent only when pace data is unavailable (no reset time or period).
    /// </summary>
    public double SecondaryColorPercent => this._presentation?.DualBucketSecondaryColorPercent
                                          ?? this.SecondaryUsedPercent;

    public string? ResetBadgeText
    {
        get
        {
            if (this._presentation == null)
            {
                return null;
            }

            IReadOnlyList<DateTime> resetTimes;
            if (this._presentation.HasDualBuckets && !this.ShowDualQuotaBars)
            {
                var preferredKind = MainWindowRuntimeLogic.GetPreferredDualBucketKind(
                    this._presentation,
                    this.DualQuotaSingleBarMode);
                resetTimes = preferredKind.HasValue
                    ? MainWindowRuntimeLogic.ResolveResetTimesForWindow(this.Usage, preferredKind.Value)
                    : Array.Empty<DateTime>();
            }
            else
            {
                var suppressSingle = this._presentation.SuppressSingleResetTime;
                resetTimes = MainWindowRuntimeLogic.ResolveResetTimes(this.Usage, suppressSingle);
            }

            if (resetTimes.Count == 0)
            {
                return null;
            }

            var resetParts = resetTimes.Select(t => this.UseRelativeResetTime ? UsageMath.FormatRelativeTime(t) : UsageMath.FormatAbsoluteTime(t)).ToList();
            return $"({string.Join(" | ", resetParts)})";
        }
    }

    public DateTime? NextResetTime => this.Usage.NextResetTime;

    public string? TooltipContent => MainWindowRuntimeLogic.BuildTooltipContent(this.Usage, this.DisplayName);

    /// <summary>
    /// Gets a formatted req/hr badge string when ShowUsagePerHour is enabled and data is available,
    /// or null (causing the badge to collapse via NullToVisibilityConverter).
    /// </summary>
    public string? UsageRateBadgeText
    {
        get
        {
            if (!this.ShowUsagePerHour || this.Usage.UsagePerHour is null)
            {
                return null;
            }

            return $"{this.Usage.UsagePerHour.Value:F1}/hr";
        }
    }

    public bool HasDetails => this.Details.Count > 0;

    /// <summary>
    /// Gets pace-adjusted used percentage for progress-bar colour decisions.
    /// PeriodDuration and NextResetTime are set on every usage by ProviderUsageDisplayCatalog
    /// before the ViewModel is constructed, so no catalog lookup or fallback is needed here.
    /// </summary>
    public double ColorIndicatorPercent
    {
        get
        {
            if (this._presentation?.HasDualBuckets == true && !this.ShowDualQuotaBars)
            {
                return this.GetSingleBarDualQuotaColorPercent();
            }

            return GetColorIndicatorPercent(
                this.Usage,
                this.UsedPercent,
                this.EnablePaceAdjustment);
        }
    }

    /// <summary>
    /// Gets the full pace badge result (tier + projected percent), or null when unavailable.
    /// </summary>
    public PaceBadgeResult? PaceBadge
    {
        get
        {
            return UsageMath.GetPaceBadge(
                this.UsedPercent,
                this.EnablePaceAdjustment,
                this.Usage.NextResetTime,
                this.Usage.PeriodDuration);
        }
    }

    /// <summary>
    /// Gets the pace badge display text, or null when unavailable.
    /// </summary>
    public string? PaceBadgeText => this.PaceBadge?.Text;

    /// <summary>
    /// Gets the projected usage text (e.g. "Projected: 73%"), or null when unavailable.
    /// </summary>
    public string? ProjectedUsageText => this.PaceBadge?.ProjectedText;

    partial void OnUsageChanged(ProviderUsage value)
    {
        UpdatePresentation();
        PopulateDetails();
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AccountDisplay));
        OnPropertyChanged(nameof(HasAccountName));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(UsedPercent));
        OnPropertyChanged(nameof(RemainingPercent));
        OnPropertyChanged(nameof(ShouldShowProgress));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusTone));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(IsRateLimited));
        OnPropertyChanged(nameof(IsQuotaBased));
        OnPropertyChanged(nameof(HasDualQuotaBuckets));
        OnPropertyChanged(nameof(PrimaryUsedPercent));
        OnPropertyChanged(nameof(SecondaryUsedPercent));
        OnPropertyChanged(nameof(PrimaryColorPercent));
        OnPropertyChanged(nameof(SecondaryColorPercent));
        OnPropertyChanged(nameof(ResetBadgeText));
        OnPropertyChanged(nameof(NextResetTime));
        OnPropertyChanged(nameof(TooltipContent));
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(UsageRateBadgeText));
        OnPropertyChanged(nameof(ColorIndicatorPercent));
        OnPropertyChanged(nameof(PaceBadge));
        OnPropertyChanged(nameof(PaceBadgeText));
        OnPropertyChanged(nameof(ProjectedUsageText));
    }

    partial void OnIsPrivacyModeChanged(bool value)
    {
        OnPropertyChanged(nameof(AccountDisplay));
    }

    partial void OnShowUsedPercentagesChanged(bool value)
    {
        UpdatePresentation();
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(StatusText));
        PopulateDetails();
    }

    partial void OnShowUsagePerHourChanged(bool value)
    {
        OnPropertyChanged(nameof(UsageRateBadgeText));
    }

    partial void OnShowDualQuotaBarsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasDualQuotaBuckets));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ColorIndicatorPercent));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResetBadgeText));
    }

    partial void OnDualQuotaSingleBarModeChanged(DualQuotaSingleBarMode value)
    {
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ColorIndicatorPercent));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResetBadgeText));
    }

    partial void OnEnablePaceAdjustmentChanged(bool value)
    {
        OnPropertyChanged(nameof(ColorIndicatorPercent));
        OnPropertyChanged(nameof(PaceBadgeText));
    }

    private void UpdatePresentation()
    {
        this._presentation = MainWindowRuntimeLogic.Create(this.Usage, this.ShowUsedPercentages);
    }

    private void PopulateDetails()
    {
        this.Details.Clear();

        var displayableDetails = MainWindowRuntimeLogic.GetDisplayableDetails(this.Usage);
        foreach (var detail in displayableDetails)
        {
            this.Details.Add(new SubProviderCardViewModel(detail, this.Usage.IsQuotaBased, this.IsPrivacyMode, this.ShowUsedPercentages));
        }
    }

    private static double GetColorIndicatorPercent(
        ProviderUsage usage,
        double usedPercent,
        bool enablePaceAdjustment,
        DateTime? nowUtc = null)
    {
        return UsageMath.GetColorIndicatorPercent(
            usedPercent,
            enablePaceAdjustment,
            usage.NextResetTime,
            usage.PeriodDuration,
            nowUtc);
    }

    private double GetSingleBarDualQuotaUsedPercent()
    {
        if (this._presentation == null || !this._presentation.HasDualBuckets)
        {
            return this.UsedPercent;
        }

        var usePrimary = MainWindowRuntimeLogic.ShouldUsePrimaryDualBucket(
            this._presentation,
            this.DualQuotaSingleBarMode);
        return usePrimary
            ? this._presentation.DualBucketPrimaryUsed!.Value
            : this._presentation.DualBucketSecondaryUsed!.Value;
    }

    private double GetSingleBarDualQuotaColorPercent()
    {
        if (this._presentation == null || !this._presentation.HasDualBuckets)
        {
            return GetColorIndicatorPercent(
                this.Usage,
                this.UsedPercent,
                this.EnablePaceAdjustment);
        }

        var usePrimary = MainWindowRuntimeLogic.ShouldUsePrimaryDualBucket(
            this._presentation,
            this.DualQuotaSingleBarMode);
        if (usePrimary)
        {
            return this._presentation.DualBucketPrimaryColorPercent ?? this._presentation.DualBucketPrimaryUsed!.Value;
        }

        return this._presentation.DualBucketSecondaryColorPercent ?? this._presentation.DualBucketSecondaryUsed!.Value;
    }


    partial void OnUseRelativeResetTimeChanged(bool value)
    {
        OnPropertyChanged(nameof(ResetBadgeText));
    }
}
