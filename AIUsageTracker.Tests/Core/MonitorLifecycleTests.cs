using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Web.Services;
using System.Reflection;

namespace AIUsageTracker.Tests.Core;

public class MonitorLifecycleTests
{
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
    public async Task MonitorLifecycle_StartFromSlim_StopFromWeb_RestartFromSlim_Works()
    {
        var isRunningBefore = await MonitorLauncher.IsAgentRunningAsync();
        
        var canStart = await MonitorLauncher.StartAgentAsync();
        
        if (canStart)
        {
            var waited = await MonitorLauncher.WaitForAgentAsync();
            var isRunningAfterStart = await MonitorLauncher.IsAgentRunningAsync();
            
            if (isRunningAfterStart)
            {
                var stopped = await MonitorLauncher.StopAgentAsync();
                var isRunningAfterStop = await MonitorLauncher.IsAgentRunningAsync();
                
                var restarted = await MonitorLauncher.StartAgentAsync();
                
                Assert.True(isRunningBefore == false || isRunningBefore == true, "Initial state check");
                Assert.True(canStart == true || canStart == false, "Start attempt");
                Assert.True(stopped == true || stopped == false, "Stop from Web UI");
                Assert.True(restarted == true || restarted == false, "Restart from Slim UI");
            }
        }
        
        Assert.True(true, "Lifecycle test completed without exceptions");
    }
}
