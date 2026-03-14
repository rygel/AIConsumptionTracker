// <copyright file="ProviderUsageDetail.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AIUsageTracker.Core.Models;

public class ProviderUsageDetail
{
    private static readonly Regex PercentagePattern = new(
        @"(?<percent>\d+(?:\.(?<fraction>\d+))?)\s*%",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private string _used = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Used
    {
        get => !string.IsNullOrWhiteSpace(this._used) ? this._used : this.GetCompatibilityDisplayValue();
        set
        {
            this._used = value ?? string.Empty;
            this.ApplyLegacyPercentCompatibility(this._used);
        }
    }

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

    public bool IsDisplayableSubProviderDetail()
    {
        return this.DetailType == ProviderUsageDetailType.Model || this.DetailType == ProviderUsageDetailType.Other || this.DetailType == ProviderUsageDetailType.RateLimit;
    }

    public void SetPercentageValue(double percentage, PercentageValueSemantic semantic, int decimalPlaces = 0, string? compatibilityText = null)
    {
        this.PercentageValue = UsageMath.ClampPercent(percentage);
        this.PercentageSemantic = semantic;
        this.PercentageDecimalPlaces = Math.Max(0, decimalPlaces);
        this._used = compatibilityText ?? string.Empty;
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

    private string GetCompatibilityDisplayValue()
    {
        if (!this.TryGetPercentageValue(out var percentage, out var semantic, out var decimalPlaces))
        {
            return string.Empty;
        }

        var format = $"F{decimalPlaces}";
        var percentText = percentage.ToString(format, CultureInfo.InvariantCulture);
        return semantic switch
        {
            PercentageValueSemantic.Used => $"{percentText}% used",
            PercentageValueSemantic.Remaining => $"{percentText}% remaining",
            _ => $"{percentText}%",
        };
    }

    private void ApplyLegacyPercentCompatibility(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var match = PercentagePattern.Match(value);
        if (!match.Success)
        {
            this.PercentageValue = null;
            this.PercentageSemantic = PercentageValueSemantic.None;
            this.PercentageDecimalPlaces = 0;
            return;
        }

        if (!double.TryParse(
                match.Groups["percent"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return;
        }

        this.PercentageValue = UsageMath.ClampPercent(parsed);
        this.PercentageSemantic = value.Contains("remaining", StringComparison.OrdinalIgnoreCase)
            ? PercentageValueSemantic.Remaining
            : value.Contains("used", StringComparison.OrdinalIgnoreCase)
                ? PercentageValueSemantic.Used
                : PercentageValueSemantic.None;
        this.PercentageDecimalPlaces = match.Groups["fraction"].Success
            ? match.Groups["fraction"].Value.Length
            : 0;
    }
}
