using System.Text.Json;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;
using Xunit;

namespace AIUsageTracker.Tests.Models;

public class ConfigTypeTests
{
    #region ProviderConfig JSON Round-trip Tests

    [Fact]
    public void ProviderConfig_DefaultTypeIsUsageBased()
    {
        var config = new ProviderConfig();
        
        Assert.Equal(ConfigType.UsageBased, config.Type);
    }

    [Fact]
    public void ProviderConfig_RoundTripsQuotaType()
    {
        var original = new ProviderConfig 
        { 
            ProviderId = "test",
            Type = ConfigType.Quota 
        };
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ProviderConfig>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal(ConfigType.Quota, deserialized!.Type);
    }

    [Fact]
    public void ProviderConfig_RoundTripsUsageBasedType()
    {
        var original = new ProviderConfig 
        { 
            ProviderId = "test",
            Type = ConfigType.UsageBased 
        };
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ProviderConfig>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal(ConfigType.UsageBased, deserialized!.Type);
    }

    [Fact]
    public void ProviderConfig_WithAllProperties_RoundTripsCorrectly()
    {
        var original = new ProviderConfig
        {
            ProviderId = "test-provider",
            ApiKey = "test-key",
            Type = ConfigType.Quota,
            PlanType = PlanType.Coding,
            ShowInTray = true,
            EnableNotifications = true,
            BaseUrl = "https://api.example.com",
            Description = "Test description"
        };
        
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ProviderConfig>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal("test-provider", deserialized!.ProviderId);
        Assert.Equal("test-key", deserialized.ApiKey);
        Assert.Equal(ConfigType.Quota, deserialized.Type);
        Assert.Equal(PlanType.Coding, deserialized.PlanType);
        Assert.True(deserialized.ShowInTray);
        Assert.True(deserialized.EnableNotifications);
        Assert.Equal("https://api.example.com", deserialized.BaseUrl);
    }

    #endregion

    #region ProviderDefinition Tests

    [Fact]
    public void ProviderDefinition_AcceptsConfigTypeEnum()
    {
        var definition = new ProviderDefinition(
            providerId: "test-provider",
            displayName: "Test Provider",
            planType: PlanType.Coding,
            isQuotaBased: true,
            defaultConfigType: ConfigType.Quota);
        
        Assert.Equal(ConfigType.Quota, definition.DefaultConfigType);
    }

    [Fact]
    public void ProviderDefinition_StoresUsageBasedConfigType()
    {
        var definition = new ProviderDefinition(
            providerId: "test-provider",
            displayName: "Test Provider",
            planType: PlanType.Usage,
            isQuotaBased: false,
            defaultConfigType: ConfigType.UsageBased);
        
        Assert.Equal(ConfigType.UsageBased, definition.DefaultConfigType);
    }

    [Fact]
    public void ProviderDefinition_AllProvidersUseValidConfigType()
    {
        foreach (var definition in ProviderMetadataCatalog.Definitions)
        {
            Assert.True(
                definition.DefaultConfigType == ConfigType.Quota || 
                definition.DefaultConfigType == ConfigType.UsageBased,
                $"Provider '{definition.ProviderId}' has invalid ConfigType: {definition.DefaultConfigType}");
        }
    }

    #endregion

    #region Enum Value Tests

    [Fact]
    public void ConfigType_HasTwoValues()
    {
        var values = Enum.GetValues(typeof(ConfigType));
        
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void ConfigType_QuotaValueIsZero()
    {
        Assert.Equal(0, (int)ConfigType.Quota);
    }

    [Fact]
    public void ConfigType_UsageBasedValueIsOne()
    {
        Assert.Equal(1, (int)ConfigType.UsageBased);
    }

    #endregion
}
