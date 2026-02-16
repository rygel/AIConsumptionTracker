using System.Diagnostics;

namespace AIConsumptionTracker.Web.Services;

public class AgentProcessService
{
    private readonly string _portFilePath;
    
    public AgentProcessService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _portFilePath = Path.Combine(appData, "AIConsumptionTracker", "Agent", "agent.port");
    }

    public async Task<(bool isRunning, int port)> GetAgentStatusAsync()
    {
        var port = await GetPortFromFileAsync();
        if (port == 0) port = 5000;
        
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            var response = await client.GetAsync($"http://localhost:{port}/api/health");
            return (response.IsSuccessStatusCode, port);
        }
        catch
        {
            return (false, port);
        }
    }

    public async Task<bool> StartAgentAsync()
    {
        var (isRunning, _) = await GetAgentStatusAsync();
        if (isRunning) return true;
        
        var port = await GetPortFromFileAsync();
        if (port == 0) port = 5000;
        
        var agentPath = FindAgentExecutable();
        if (agentPath == null) return false;
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = agentPath,
                Arguments = $"--urls \"http://localhost:{port}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(agentPath)
            };
            
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? FindAgentExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        
        var paths = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "AIConsumptionTracker.Agent", "bin", "Debug", "net8.0", "AIConsumptionTracker.Agent.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "AIConsumptionTracker.Agent", "bin", "Release", "net8.0", "AIConsumptionTracker.Agent.exe"),
            Path.Combine(baseDir, "AIConsumptionTracker.Agent.exe"),
        };
        
        return paths.FirstOrDefault(File.Exists);
    }

    private async Task<int> GetPortFromFileAsync()
    {
        try
        {
            if (File.Exists(_portFilePath))
            {
                var portStr = await File.ReadAllTextAsync(_portFilePath);
                return int.TryParse(portStr, out var port) ? port : 0;
            }
        }
        catch { }
        return 0;
    }
}
