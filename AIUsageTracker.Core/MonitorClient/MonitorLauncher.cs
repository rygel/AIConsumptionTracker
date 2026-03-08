using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using AIUsageTracker.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.Core.MonitorClient;

public class MonitorLauncher
{
    private const int DefaultPort = 5000;
    private const int MaxWaitSeconds = 30;
    private const int StopWaitSeconds = 5;
    private static ILogger<MonitorLauncher>? _logger;
    private static readonly SemaphoreSlim StartupSemaphore = new(1, 1);
    private static Func<IEnumerable<string>>? _monitorInfoCandidatePathsOverride;
    private static Func<int, Task<bool>>? _healthCheckOverride;
    private static Func<int, Task<bool>>? _processRunningOverride;
    private static Func<int, Task<bool>>? _stopProcessOverride;
    private static Func<Task<bool>>? _stopNamedProcessesOverride;

    public static void SetLogger(ILogger<MonitorLauncher> logger) => _logger = logger;

    internal static IDisposable PushTestOverrides(
        IEnumerable<string>? monitorInfoCandidatePaths = null,
        Func<int, Task<bool>>? healthCheckAsync = null,
        Func<int, Task<bool>>? processRunningAsync = null,
        Func<int, Task<bool>>? stopProcessAsync = null,
        Func<Task<bool>>? stopNamedProcessesAsync = null)
    {
        var previousCandidatePaths = _monitorInfoCandidatePathsOverride;
        var previousHealthCheck = _healthCheckOverride;
        var previousProcessCheck = _processRunningOverride;
        var previousStopProcess = _stopProcessOverride;
        var previousStopNamedProcesses = _stopNamedProcessesOverride;

        if (monitorInfoCandidatePaths != null)
        {
            var paths = monitorInfoCandidatePaths.ToArray();
            _monitorInfoCandidatePathsOverride = () => paths;
        }

        if (healthCheckAsync != null)
        {
            _healthCheckOverride = healthCheckAsync;
        }

        if (processRunningAsync != null)
        {
            _processRunningOverride = processRunningAsync;
        }

        if (stopProcessAsync != null)
        {
            _stopProcessOverride = stopProcessAsync;
        }

        if (stopNamedProcessesAsync != null)
        {
            _stopNamedProcessesOverride = stopNamedProcessesAsync;
        }

        return new TestOverrideScope(() =>
        {
            _monitorInfoCandidatePathsOverride = previousCandidatePaths;
            _healthCheckOverride = previousHealthCheck;
            _processRunningOverride = previousProcessCheck;
            _stopProcessOverride = previousStopProcess;
            _stopNamedProcessesOverride = previousStopNamedProcesses;
        });
    }


    private static async Task<(MonitorInfo? Info, string? Path)> ReadAgentInfoAsync()
    {
        string? path = null;

        try
        {
            path = GetExistingAgentInfoPath();

            if (path != null)
            {
                if (MonitorInfoPathCatalog.IsDeprecatedReadPath(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    path))
                {
                    MonitorService.LogDiagnostic($"Using deprecated monitor metadata path '{path}'. Rewrite will occur at the canonical AIUsageTracker path.");
                }

                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                var info = JsonSerializer.Deserialize<MonitorInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (info != null)
                {
                    return (info, path);
                }

                await QuarantineMonitorInfoAsync(path, $"Monitor metadata at '{path}' was empty or invalid; invalidating metadata.").ConfigureAwait(false);
            }
            
            return (null, path);
        }
        catch (JsonException ex)
        {
            MonitorService.LogDiagnostic($"Failed to parse monitor metadata: {ex.Message}");
            if (path != null)
            {
                await QuarantineMonitorInfoAsync(path).ConfigureAwait(false);
            }

            return (null, path);
        }
        catch (IOException ex)
        {
            MonitorService.LogDiagnostic($"Failed to read monitor metadata: {ex.Message}");
            return (null, path);
        }
        catch (UnauthorizedAccessException ex)
        {
            MonitorService.LogDiagnostic($"Access denied reading monitor metadata: {ex.Message}");
            return (null, path);
        }
        catch
        {
            MonitorService.LogDiagnostic("Failed to load monitor metadata for an unknown reason.");
            return (null, path);
        }
    }

    private static async Task<(MonitorInfo? Info, string? Path)> ReadValidatedAgentInfoAsync()
    {
        var (info, path) = await ReadAgentInfoAsync().ConfigureAwait(false);
        if (info != null)
        {
            var healthOk = await CheckHealthAsync(info.Port).ConfigureAwait(false);
            var processRunning = await CheckProcessRunningAsync(info.ProcessId).ConfigureAwait(false);

            if (healthOk && processRunning)
            {
                return (info, path);
            }

            MonitorService.LogDiagnostic($"Monitor metadata stale: health={healthOk}, processRunning={processRunning}, invalidating metadata");
            if (path != null)
            {
                await QuarantineMonitorInfoAsync(path).ConfigureAwait(false);
            }
        }

        return (null, path);
    }

    private static async Task<(MonitorInfo? Info, int Port, bool IsRunning)> ResolveMonitorStateAsync()
    {
        var (info, _) = await ReadValidatedAgentInfoAsync().ConfigureAwait(false);
        if (info != null)
        {
            return (info, info.Port, true);
        }

        var port = DefaultPort;
        var isRunning = await CheckHealthAsync(port).ConfigureAwait(false);
        return (null, port, isRunning);
    }

    public static async Task<int> GetAgentPortAsync()
    {
        var (_, port, _) = await ResolveMonitorStateAsync().ConfigureAwait(false);
        return port;
    }

    public static async Task<bool> IsAgentRunningAsync()
    {
        var readyPort = await GetReadyPortAsync().ConfigureAwait(false);
        return readyPort.HasValue;
    }
    
    public static async Task<(bool IsRunning, int Port)> IsAgentRunningWithPortAsync()
    {
        var (port, isRunning) = await GetReadyStateAsync().ConfigureAwait(false);
        return (isRunning, port);
    }

    private static async Task<bool> CheckHealthAsync(int port)
    {
        if (_healthCheckOverride != null)
        {
            return await _healthCheckOverride(port).ConfigureAwait(false);
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
            var response = await client.GetAsync($"http://localhost:{port}/api/health").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            MonitorService.LogDiagnostic($"Health check request failed on port {port}: {ex.Message}");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            MonitorService.LogDiagnostic($"Health check timed out on port {port}: {ex.Message}");
            return false;
        }
        catch
        {
            MonitorService.LogDiagnostic($"Health check failed on port {port} for an unknown reason.");
            return false;
        }
    }

    private static Task<bool> CheckProcessRunningAsync(int processId)
    {
        if (_processRunningOverride != null)
        {
            return _processRunningOverride(processId);
        }

        if (processId <= 0)
        {
            return Task.FromResult(false);
        }

        try
        {
            var process = Process.GetProcessById(processId);
            return Task.FromResult(!process.HasExited);
        }
        catch (ArgumentException)
        {
            MonitorService.LogDiagnostic($"Monitor process {processId} was not found.");
            return Task.FromResult(false);
        }
        catch
        {
            MonitorService.LogDiagnostic($"Failed to query monitor process {processId}.");
            return Task.FromResult(false);
        }
    }

    public static async Task<MonitorInfo?> GetAndValidateMonitorInfoAsync()
    {
        var (info, _) = await ReadValidatedAgentInfoAsync().ConfigureAwait(false);
        return info;
    }

    public static Task InvalidateMonitorInfoAsync()
    {
        try
        {
            foreach (var infoPath in GetExistingAgentInfoPaths())
            {
                InvalidateMonitorInfoPath(infoPath);
            }
        }
        catch (Exception ex)
        {
            MonitorService.LogDiagnostic($"Failed to invalidate monitor metadata: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static Task QuarantineMonitorInfoAsync(string infoPath, string? diagnosticMessage = null)
    {
        if (!string.IsNullOrEmpty(diagnosticMessage))
        {
            MonitorService.LogDiagnostic(diagnosticMessage);
        }

        InvalidateMonitorInfoPath(infoPath);
        return Task.CompletedTask;
    }

    private static void InvalidateMonitorInfoPath(string infoPath)
    {
        var backupPath = infoPath + ".stale." + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        File.Move(infoPath, backupPath, overwrite: true);
        MonitorService.LogDiagnostic($"Backed up stale metadata to: {backupPath}");
    }

    public static async Task<bool> StartAgentAsync()
    {
        await StartupSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var (validatedInfo, port, isRunning) = await ResolveMonitorStateAsync().ConfigureAwait(false);
            if (validatedInfo != null)
            {
                MonitorService.LogDiagnostic($"Monitor already running on port {validatedInfo.Port}; skipping start.");
                return true;
            }

            if (isRunning)
            {
                MonitorService.LogDiagnostic($"Monitor already responding on port {port}; skipping start.");
                return true;
            }

            var launchPlan = TryResolveLaunchPlan(port);
            if (launchPlan == null)
            {
                return false;
            }

            return TryStartMonitorProcess(launchPlan.Value.StartInfo, launchPlan.Value.LaunchTarget);
        }
        catch (Exception ex)
        {
            MonitorService.LogDiagnostic($"Failed to start Monitor: {ex.Message}");
            return false;
        }
        finally
        {
            StartupSemaphore.Release();
        }
    }

    private static (ProcessStartInfo StartInfo, string LaunchTarget)? TryResolveLaunchPlan(int port)
    {
        var monitorExeName = OperatingSystem.IsWindows()
            ? "AIUsageTracker.Monitor.exe"
            : "AIUsageTracker.Monitor";
        var possiblePaths = MonitorExecutableCatalog.GetExecutableCandidates(AppContext.BaseDirectory, monitorExeName);

        MonitorService.LogDiagnostic($"Locating Monitor executable (checked {possiblePaths.Count} common locations)...");
        var agentPath = possiblePaths.FirstOrDefault(File.Exists);

        if (agentPath != null)
        {
            MonitorService.LogDiagnostic($"Monitor executable found at: {agentPath}. Launching...");
            return (CreateExecutableLaunchInfo(agentPath, port), agentPath);
        }

        MonitorService.LogDiagnostic("Monitor executable not found. Searching for project directory for 'dotnet run'...");
        var agentProjectDir = MonitorExecutableCatalog.FindProjectDirectory(AppContext.BaseDirectory);
        if (agentProjectDir == null)
        {
            MonitorService.LogDiagnostic("Could not find Monitor executable or project directory.");
            return null;
        }

        MonitorService.LogDiagnostic($"Found Monitor project at: {agentProjectDir}. Launching via 'dotnet run'...");
        return (CreateProjectLaunchInfo(agentProjectDir, port), "dotnet run");
    }

    private static ProcessStartInfo CreateExecutableLaunchInfo(string agentPath, int port)
    {
        return new ProcessStartInfo
        {
            FileName = agentPath,
            Arguments = $"--urls \"http://localhost:{port}\" --debug",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(agentPath),
        };
    }

    private static ProcessStartInfo CreateProjectLaunchInfo(string agentProjectDir, int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{agentProjectDir}\" --urls \"http://localhost:{port}\" -- --debug",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = agentProjectDir,
        };

        // Prevent MSBuild from leaving zombie processes that hold file locks
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        return startInfo;
    }

    private static bool TryStartMonitorProcess(ProcessStartInfo startInfo, string launchTarget)
    {
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                MonitorService.LogDiagnostic($"Monitor launch returned no process for target '{launchTarget}'.");
                return false;
            }

            MonitorService.LogDiagnostic($"Monitor process started via '{launchTarget}' (PID {process.Id}).");
            return true;
        }
        catch (Exception ex)
        {
            MonitorService.LogDiagnostic($"Failed to launch Monitor via '{launchTarget}': {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> EnsureAgentRunningAsync(CancellationToken cancellationToken = default)
    {
        var readyPort = await GetReadyPortAsync().ConfigureAwait(false);
        if (readyPort.HasValue)
        {
            MonitorService.LogDiagnostic($"Monitor already ready on port {readyPort.Value}; no startup needed.");
            return true;
        }

        var started = await StartAgentAsync().ConfigureAwait(false);
        if (!started)
        {
            return false;
        }

        return await WaitForAgentAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<bool> StopAgentAsync()
    {
        try
        {
            var (info, _) = await ReadValidatedAgentInfoAsync().ConfigureAwait(false);
            var targetPort = info?.Port > 0 ? info.Port : DefaultPort;
            if (await TryStopKnownMonitorProcessAsync(info).ConfigureAwait(false))
            {
                await InvalidateMonitorInfoAsync().ConfigureAwait(false);
                return true;
            }

            if (await TryStopNamedProcessesAsync().ConfigureAwait(false))
            {
                await InvalidateMonitorInfoAsync().ConfigureAwait(false);
                return true;
            }

            var isStillHealthy = await CheckHealthAsync(targetPort).ConfigureAwait(false);
            if (!isStillHealthy)
            {
                await InvalidateMonitorInfoAsync().ConfigureAwait(false);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop Agent");
            return false;
        }
    }

    private static async Task<bool> TryStopKnownMonitorProcessAsync(MonitorInfo? info)
    {
        if (info?.ProcessId > 0)
        {
            return await TryStopProcessAsync(info.ProcessId).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> TryStopNamedProcessesAsync()
    {
        if (_stopNamedProcessesOverride != null)
        {
            return await _stopNamedProcessesOverride().ConfigureAwait(false);
        }

        var processes = Process.GetProcessesByName("AIUsageTracker.Monitor")
            .ToArray();
        var stoppedAny = false;
        foreach (var process in processes)
        {
            using (process)
            {
                if (await TryStopProcessAsync(process).ConfigureAwait(false))
                {
                    stoppedAny = true;
                }
            }
        }

        return stoppedAny;
    }

    private static async Task<bool> TryStopProcessAsync(int processId)
    {
        if (_stopProcessOverride != null)
        {
            return await _stopProcessOverride(processId).ConfigureAwait(false);
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return await TryStopProcessAsync(process).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (Exception ex)
        {
            MonitorService.LogDiagnostic($"Failed to stop process {processId}: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> TryStopProcessAsync(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return true;
            }

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(StopWaitSeconds)).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            MonitorService.LogDiagnostic($"Timed out waiting for process {process.Id} to exit.");
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (Exception ex)
        {
            MonitorService.LogDiagnostic($"Failed to stop process {process.Id}: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> WaitForAgentAsync(CancellationToken cancellationToken = default)
    {
        MonitorService.LogDiagnostic($"Waiting for Monitor to start (max {MaxWaitSeconds}s)...");
        var stopwatch = Stopwatch.StartNew();
        int attempt = 0;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(MaxWaitSeconds))
        {
            attempt++;
            var readyPort = await GetReadyPortAsync().ConfigureAwait(false);
            if (readyPort.HasValue)
            {
                MonitorService.LogDiagnostic($"Monitor is ready on port {readyPort.Value} after {stopwatch.Elapsed.TotalSeconds:F1}s.");
                return true;
            }

            if (attempt % 5 == 0) // Log status every 1 second (5 * 200ms)
            {
                MonitorService.LogDiagnostic($"Still waiting for Monitor... (elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s)");
            }

            try
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                MonitorService.LogDiagnostic($"Monitor wait cancelled after {stopwatch.Elapsed.TotalSeconds:F1}s.");
                return false;
            }
        }

        MonitorService.LogDiagnostic("Timed out waiting for Monitor.");
        return false;
    }

    private static async Task<int?> GetReadyPortAsync()
    {
        var (port, isRunning) = await GetReadyStateAsync().ConfigureAwait(false);
        return isRunning ? port : null;
    }

    private static async Task<(int Port, bool IsRunning)> GetReadyStateAsync()
    {
        var (info, port, isRunning) = await ResolveMonitorStateAsync().ConfigureAwait(false);
        if (info != null)
        {
            MonitorService.LogDiagnostic($"Monitor is running on port {info.Port}");
            return (info.Port, true);
        }

        MonitorService.LogDiagnostic($"Checking Monitor status on port: {port}");
        if (isRunning)
        {
            MonitorService.LogDiagnostic($"Monitor is running on port {port}");
            return (port, true);
        }

        MonitorService.LogDiagnostic($"Monitor not found on port {port}.");
        return (port, false);
    }

    private static string? GetExistingAgentInfoPath()
    {
        return GetExistingAgentInfoPaths().FirstOrDefault();
    }

    private static IEnumerable<string> GetExistingAgentInfoPaths()
    {
        return GetMonitorInfoCandidatePaths()
            .Where(File.Exists)
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path));
    }

    private static IEnumerable<string> GetMonitorInfoCandidatePaths()
    {
        if (_monitorInfoCandidatePathsOverride != null)
        {
            return _monitorInfoCandidatePathsOverride();
        }

        return MonitorInfoPathCatalog.GetReadCandidatePathsFromEnvironment();
    }

    private sealed class TestOverrideScope : IDisposable
    {
        private readonly Action _reset;
        private bool _disposed;

        public TestOverrideScope(Action reset)
        {
            this._reset = reset;
        }

        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            this._reset();
        }
    }
}
