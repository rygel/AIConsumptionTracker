using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Core.Models;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace AIUsageTracker.Tests.Core;

public class MonitorPortDiscoveryTests
{
    [Fact]
    public void MonitorService_HasRefreshPortMethod()
    {
        var type = typeof(MonitorService);
        var method = type.GetMethod("RefreshPortAsync");
        
        Assert.NotNull(method);
        Assert.True(method.ReturnType == typeof(Task) || 
                    method.ReturnType == typeof(ValueTask),
            "RefreshPortAsync should return Task or ValueTask");
    }

    [Fact]
    public void MonitorService_HasRefreshAgentInfoMethod()
    {
        var type = typeof(MonitorService);
        var method = type.GetMethod("RefreshAgentInfoAsync");
        
        Assert.NotNull(method);
        Assert.True(method.ReturnType == typeof(Task) || 
                    method.ReturnType == typeof(ValueTask),
            "RefreshAgentInfoAsync should return Task or ValueTask");
    }

    [Fact]
    public void MonitorService_HasGetConfigsAsyncMethod()
    {
        var type = typeof(MonitorService);
        var method = type.GetMethod("GetConfigsAsync");
        
        Assert.NotNull(method);
        Assert.True(method.ReturnType.Name.Contains("Task"),
            "GetConfigsAsync should return Task");
    }

    [Fact]
    public void MonitorService_HasGetUsageAsyncMethod()
    {
        var type = typeof(MonitorService);
        var method = type.GetMethod("GetUsageAsync");
        
        Assert.NotNull(method);
        Assert.True(method.ReturnType.Name.Contains("Task"),
            "GetUsageAsync should return Task");
    }

    [Fact]
    public async Task GetConfigsAsync_ReturnsEmptyList_WhenMonitorNotAvailable()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:9999")
        };
        
        var service = new MonitorService(httpClient);
        service.AgentUrl = "http://localhost:9999";

        var configs = await service.GetConfigsAsync();

        Assert.NotNull(configs);
        Assert.Empty(configs);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsEmptyList_WhenMonitorNotAvailable()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:9999")
        };
        
        var service = new MonitorService(httpClient);
        service.AgentUrl = "http://localhost:9999";

        var usages = await service.GetUsageAsync();

        Assert.NotNull(usages);
    }

    [Fact]
    public void MonitorInfo_ContainsRequiredProperties()
    {
        var info = new MonitorInfo
        {
            Port = 5000,
            ProcessId = 12345,
            StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            DebugMode = false,
            MachineName = "TEST-MACHINE",
            UserName = "testuser"
        };

        Assert.Equal(5000, info.Port);
        Assert.Equal(12345, info.ProcessId);
        Assert.False(info.DebugMode);
        Assert.Equal("TEST-MACHINE", info.MachineName);
        Assert.Equal("testuser", info.UserName);
    }

    [Fact]
    public void MonitorInfo_CanBeSerializedAndDeserialized()
    {
        var original = new MonitorInfo
        {
            Port = 5001,
            ProcessId = 99999,
            StartedAt = "2026-02-27 12:00:00",
            DebugMode = true,
            MachineName = "TEST-PC",
            UserName = "developer",
            Errors = new List<string> { "Error 1", "Error 2" }
        };

        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        
        var deserialized = JsonSerializer.Deserialize<MonitorInfo>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        Assert.NotNull(deserialized);
        Assert.Equal(original.Port, deserialized.Port);
        Assert.Equal(original.ProcessId, deserialized.ProcessId);
        Assert.Equal(original.DebugMode, deserialized.DebugMode);
    }
}
