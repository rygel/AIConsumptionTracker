// <copyright file="ProviderUsageDetail.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace AIUsageTracker.Core.Models;

public class ProviderUsageDetail
{
    public string Name { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PercentageValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonConverter(typeof(JsonStringEnumConverter<PercentageValueSemantic>))]
    public PercentageValueSemantic PercentageSemantic { get; set; } = PercentageValueSemantic.None;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PercentageDecimalPlaces { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime? NextResetTime { get; set; }

    public ProviderUsageDetailType DetailType { get; set; } = ProviderUsageDetailType.Unknown;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsStale { get; set; }

    [JsonPropertyName("window_kind")]
    public WindowKind QuotaBucketKind { get; set; } = WindowKind.None;

    /// <summary>
    /// Duration of the quota window (e.g. 5 hours for burst, 7 days for weekly rolling).
    /// Used by the UI to compute time-adjusted pace and suppress false-positive alerts
    /// when usage is below the expected pace for the elapsed fraction of the period.
    /// Null when the window duration is unknown.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? PeriodDuration { get; set; }

    public bool IsDisplayableSubProviderDetail()
    {
        return this.DetailType == ProviderUsageDetailType.Model || this.DetailType == ProviderUsageDetailType.Other || this.DetailType == ProviderUsageDetailType.RateLimit;
    }

    public void SetPercentageValue(double percentage, PercentageValueSemantic semantic, int decimalPlaces = 0)
    {
        this.PercentageValue = UsageMath.ClampPercent(percentage);
        this.PercentageSemantic = semantic;
        this.PercentageDecimalPlaces = Math.Max(0, decimalPlaces);
    }

    public bool TryGetPercentageValue(out double percentage, out PercentageValueSemantic semantic, out int decimalPlaces)
    {
        if (!this.PercentageValue.HasValue)
        {
            percentage = 0;
            semantic = PercentageValueSemantic.None;
            decimalPlaces = 0;
            return false;
        }

        percentage = UsageMath.ClampPercent(this.PercentageValue.Value);
        semantic = this.PercentageSemantic;
        decimalPlaces = Math.Max(0, this.PercentageDecimalPlaces);
        return true;
    }
}
