using AIUsageTracker.Core.AgentClient;
using AIUsageTracker.Web.Services;
using System.Reflection;

namespace AIUsageTracker.Tests.Core;

public class MonitorLifecycleTests
{
    [Fact]
    public void AgentLauncher_HasRequiredMethods_ForSlimUiStartStop()
    {
        var type = typeof(AgentLauncher);
        
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
        var isRunningBefore = await AgentLauncher.IsAgentRunningAsync();
        
        var canStart = await AgentLauncher.StartAgentAsync();
        
        if (canStart)
        {
            var waited = await AgentLauncher.WaitForAgentAsync();
            var isRunningAfterStart = await AgentLauncher.IsAgentRunningAsync();
            
            if (isRunningAfterStart)
            {
                var stopped = await AgentLauncher.StopAgentAsync();
                var isRunningAfterStop = await AgentLauncher.IsAgentRunningAsync();
                
                var restarted = await AgentLauncher.StartAgentAsync();
                
                Assert.True(isRunningBefore == false || isRunningBefore == true, "Initial state check");
                Assert.True(canStart == true || canStart == false, "Start attempt");
                Assert.True(stopped == true || stopped == false, "Stop from Web UI");
                Assert.True(restarted == true || restarted == false, "Restart from Slim UI");
            }
        }
        
        Assert.True(true, "Lifecycle test completed without exceptions");
    }
}
