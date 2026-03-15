// <copyright file="ProviderUsageDetailTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

#pragma warning disable CS0618 // Used: legacy field tested for DB snapshot compatibility

using System.Text.Json;
using System.Text.Json.Serialization;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Tests.Models;

public class ProviderUsageDetailTests
{
    [Fact]
    public void IsDisplayableSubProviderDetail_DetailTypeQuotaWindow_ReturnsFalse()
    {
        var detail = new ProviderUsageDetail
        {
            Name = "Custom Primary",
            DetailType = ProviderUsageDetailType.QuotaWindow,
        };

        Assert.False(detail.IsDisplayableSubProviderDetail());
    }

    [Fact]
    public void IsDisplayableSubProviderDetail_DetailTypeModel_ReturnsTrue()
    {
        var detail = new ProviderUsageDetail
        {
            Name = "GPT-5",
            DetailType = ProviderUsageDetailType.Model,
        };

        Assert.True(detail.IsDisplayableSubProviderDetail());
    }

    [Fact]
    public void IsDisplayableSubProviderDetail_WithOtherDetailType_ReturnsTrue()
    {
        var detail = new ProviderUsageDetail
        {
            Name = "Avg Cost/Day",
            DetailType = ProviderUsageDetailType.Other,
            QuotaBucketKind = WindowKind.None,
        };

        Assert.True(detail.IsDisplayableSubProviderDetail());
    }

    [Fact]
    public void Serialization_UsesWindowKindContractFieldName()
    {
        var detail = new ProviderUsageDetail
        {
            Name = "Requests / Hour",
            DetailType = ProviderUsageDetailType.QuotaWindow,
            QuotaBucketKind = WindowKind.Burst,
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        var json = JsonSerializer.Serialize(detail, options);

        Assert.Contains("\"window_kind\":\"primary\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("quota_bucket_kind", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialization_FromWindowKindContractField_SetsQuotaBucketKind()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        var detail = JsonSerializer.Deserialize<ProviderUsageDetail>(
            "{\"name\":\"Requests / Day\",\"detail_type\":\"quota_window\",\"window_kind\":\"secondary\"}",
            options);

        Assert.NotNull(detail);
        Assert.Equal(WindowKind.Rolling, detail!.QuotaBucketKind);
        Assert.Equal(ProviderUsageDetailType.QuotaWindow, detail.DetailType);
    }

    [Fact]
    public void SetPercentageValue_PopulatesTypedPercentageAndCompatibilityText()
    {
        var detail = new ProviderUsageDetail();

        detail.SetPercentageValue(73.5, PercentageValueSemantic.Remaining, decimalPlaces: 1);

        Assert.True(detail.TryGetPercentageValue(out var percentage, out var semantic, out var decimalPlaces));
        Assert.Equal(73.5, percentage);
        Assert.Equal(PercentageValueSemantic.Remaining, semantic);
        Assert.Equal(1, decimalPlaces);
        Assert.Equal("73.5% remaining", detail.Used);
    }

    [Fact]
    public void Used_LegacySemanticText_BackfillsTypedPercentageFields()
    {
        var detail = new ProviderUsageDetail
        {
            Used = "42% used",
        };

        Assert.True(detail.TryGetPercentageValue(out var percentage, out var semantic, out var decimalPlaces));
        Assert.Equal(42, percentage);
        Assert.Equal(PercentageValueSemantic.Used, semantic);
        Assert.Equal(0, decimalPlaces);
    }

    [Fact]
    public void Serialization_WritesTypedPercentageFields()
    {
        var detail = new ProviderUsageDetail
        {
            Name = "Model quota",
            DetailType = ProviderUsageDetailType.Model,
            PercentageValue = 88.8,
            PercentageSemantic = PercentageValueSemantic.Remaining,
            PercentageDecimalPlaces = 1,
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        var json = JsonSerializer.Serialize(detail, options);
        var roundTripped = JsonSerializer.Deserialize<ProviderUsageDetail>(json, options);

        Assert.Contains("\"percentage_value\":88.8", json, StringComparison.Ordinal);
        Assert.Contains("\"percentage_decimal_places\":1", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        Assert.True(roundTripped!.TryGetPercentageValue(out var percentage, out var semantic, out var decimalPlaces));
        Assert.Equal(88.8, percentage);
        Assert.Equal(PercentageValueSemantic.Remaining, semantic);
        Assert.Equal(1, decimalPlaces);
    }
}
