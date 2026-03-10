// <copyright file="MonitorActionApiTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Net;
using System.Text.Json;

namespace AIUsageTracker.Web.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MonitorActionApiTests
{
    [TestMethod]
    public async Task MonitorStartEndpoint_ReturnsAlreadyRunning_WhenLauncherReportsHealthyMonitorAsync()
    {
        using var host = await TestWebHost.StartAsync(new
        {
            statusSequence = new[]
            {
                new
                {
                    isRunning = true,
                    port = 6222,
                    hasMetadata = true,
                    message = "Healthy on port 6222.",
                    error = (string?)null,
                },
            },
            ensureAgentRunningResult = false,
            stopAgentResult = false,
        }).ConfigureAwait(false);

        using var response = await host.Client.PostAsync("/api/monitor/start", content: null).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("success").GetBoolean());
        Assert.AreEqual("Monitor already running on port 6222.", root.GetProperty("message").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("error").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startupState").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startupFailureReason").ValueKind);
    }

    [TestMethod]
    public async Task MonitorStartEndpoint_ReturnsStructuredFailure_WhenLauncherReportsStartupFailureAsync()
    {
        using var host = await TestWebHost.StartAsync(new
        {
            statusSequence = new[]
            {
                new
                {
                    isRunning = false,
                    port = 5000,
                    hasMetadata = false,
                    message = "Monitor info file not found. Start Monitor to initialize it.",
                    error = "agent-info-missing",
                },
                new
                {
                    isRunning = false,
                    port = 5000,
                    hasMetadata = true,
                    message = "Startup status: failed: port bind failed",
                    error = "monitor-startup-failed",
                },
            },
            ensureAgentRunningResult = false,
            stopAgentResult = false,
        }).ConfigureAwait(false);

        using var response = await host.Client.PostAsync("/api/monitor/start", content: null).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.IsFalse(root.GetProperty("success").GetBoolean());
        Assert.AreEqual("Monitor startup failed: port bind failed", root.GetProperty("message").GetString());
        Assert.AreEqual("monitor-startup-failed", root.GetProperty("error").GetString());
        Assert.AreEqual("failed", root.GetProperty("startupState").GetString());
        Assert.AreEqual("port bind failed", root.GetProperty("startupFailureReason").GetString());
    }

    [TestMethod]
    public async Task MonitorStopEndpoint_ReturnsAlreadyStopped_WhenLauncherReportsMissingMetadataAsync()
    {
        using var host = await TestWebHost.StartAsync(new
        {
            statusSequence = new[]
            {
                new
                {
                    isRunning = false,
                    port = 5000,
                    hasMetadata = false,
                    message = "Monitor info file not found. Start Monitor to initialize it.",
                    error = "agent-info-missing",
                },
            },
            ensureAgentRunningResult = false,
            stopAgentResult = false,
        }).ConfigureAwait(false);

        using var response = await host.Client.PostAsync("/api/monitor/stop", content: null).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("success").GetBoolean());
        Assert.AreEqual("Monitor already stopped (info file missing).", root.GetProperty("message").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("error").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startupState").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startupFailureReason").ValueKind);
    }

    [TestMethod]
    public async Task MonitorStopEndpoint_ReturnsStopped_WhenLauncherStopsRunningMonitorAsync()
    {
        using var host = await TestWebHost.StartAsync(new
        {
            statusSequence = new[]
            {
                new
                {
                    isRunning = true,
                    port = 6333,
                    hasMetadata = true,
                    message = "Healthy on port 6333.",
                    error = (string?)null,
                },
            },
            ensureAgentRunningResult = false,
            stopAgentResult = true,
        }).ConfigureAwait(false);

        using var response = await host.Client.PostAsync("/api/monitor/stop", content: null).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("success").GetBoolean());
        Assert.AreEqual("Monitor stopped on port 6333.", root.GetProperty("message").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("error").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startupState").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startupFailureReason").ValueKind);
    }
}
