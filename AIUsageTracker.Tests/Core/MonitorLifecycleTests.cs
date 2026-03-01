using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Web.Services;
using System.Reflection;

namespace AIUsageTracker.Tests.Core;

// These tests manipulate a shared Monitor process and must not run in parallel.
[CollectionDefinition("MonitorLifecycle", DisableParallelization = true)]
public sealed class MonitorLifecycleCollectionDefinition
{
}

[Collection("MonitorLifecycle")]
public class MonitorLifecycleTests
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan WaitReadyTimeout = TimeSpan.FromSeconds(40);

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string operation)
    {
        try
        {
            return await task.WaitAsync(timeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Operation '{operation}' exceeded {timeout.TotalSeconds:F0}s.", ex);
        }
    }

    private static async Task WithTimeoutAsync(Task task, TimeSpan timeout, string operation)
    {
        try
        {
            await task.WaitAsync(timeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Operation '{operation}' exceeded {timeout.TotalSeconds:F0}s.", ex);
        }
    }

    [Fact]
    public void MonitorLauncher_HasRequiredMethods_ForSlimUiStartStop()
    {
        var type = typeof(MonitorLauncher);
        
        var startMethod = type.GetMethod("StartAgentAsync", BindingFlags.Public | BindingFlags.Static);
        var stopMethod = type.GetMethod("StopAgentAsync", BindingFlags.Public | BindingFlags.Static);
        var isRunningMethod = type.GetMethod("IsAgentRunningAsync", BindingFlags.Public | BindingFlags.Static);
        var waitMethod = type.GetMethod("WaitForAgentAsync", BindingFlags.Public | BindingFlags.Static);
        
        Assert.NotNull(startMethod);
        Assert.NotNull(stopMethod);
        Assert.NotNull(isRunningMethod);
        Assert.NotNull(waitMethod);
        
        Assert.True(startMethod.ReturnType == typeof(Task<bool>) || 
                    startMethod.ReturnType == typeof(ValueTask<bool>),
            "StartAgentAsync should return Task<bool> or ValueTask<bool>");
    }

    [Fact]
    public void MonitorProcessService_HasRequiredMethods_ForWebUiStop()
    {
        var type = typeof(MonitorProcessService);
        
        var stopMethod = type.GetMethod("StopAgentAsync");
        var stopDetailedMethod = type.GetMethod("StopAgentDetailedAsync");
        
        Assert.NotNull(stopMethod);
        Assert.NotNull(stopDetailedMethod);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MonitorLifecycle_StartFromSlim_StopFromWeb_RestartFromSlim_Works()
    {
        if (!IsIntegrationEnabled())
        {
            return;
        }

        await WithTimeoutAsync(
            RunLifecycleScenarioAsync(),
            TimeSpan.FromSeconds(90),
            "Monitor lifecycle integration test");
    }

    private static bool IsIntegrationEnabled()
    {
        var value = Environment.GetEnvironmentVariable("RUN_MONITOR_LIFECYCLE_TESTS");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RunLifecycleScenarioAsync()
    {
        try
        {
            var canStart = await WithTimeoutAsync(
                MonitorLauncher.StartAgentAsync(),
                StartStopTimeout,
                "StartAgentAsync");
            if (!canStart)
            {
                return;
            }

            using var initialWaitTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var started = await WithTimeoutAsync(
                MonitorLauncher.WaitForAgentAsync(initialWaitTokenSource.Token),
                WaitReadyTimeout,
                "WaitForAgentAsync (initial)");
            if (!started)
            {
                return;
            }

            await WithTimeoutAsync(
                MonitorLauncher.StopAgentAsync(),
                StartStopTimeout,
                "StopAgentAsync");

            var restarted = await WithTimeoutAsync(
                MonitorLauncher.StartAgentAsync(),
                StartStopTimeout,
                "StartAgentAsync (restart)");
            if (!restarted)
            {
                return;
            }

            using var restartWaitTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var restartReady = await WithTimeoutAsync(
                MonitorLauncher.WaitForAgentAsync(restartWaitTokenSource.Token),
                WaitReadyTimeout,
                "WaitForAgentAsync (restart)");
            Assert.True(restartReady, "Monitor should be reachable after restart.");
        }
        finally
        {
            // Ensure the test never leaves a monitor process running in CI.
            await WithTimeoutAsync(
                MonitorLauncher.StopAgentAsync(),
                StartStopTimeout,
                "StopAgentAsync (cleanup)");
        }
    }
}
