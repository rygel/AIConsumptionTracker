using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AIUsageTracker.Web.Tests;

public sealed class KestrelWebApplicationFactory<TEntryPoint> : IDisposable where TEntryPoint : class
{
    private readonly object _syncRoot = new();
    private readonly StringBuilder _startupOutput = new();
    private readonly string _projectPath;
    private Process? _process;
    private string? _serverAddress;
    private bool _disposed;
    private bool _initialized;

    public KestrelWebApplicationFactory()
    {
        _projectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AIUsageTracker.Web"));
    }

    public string ServerAddress
    {
        get
        {
            EnsureStarted();
            return _serverAddress ?? throw new InvalidOperationException("Server failed to start.");
        }
    }

    private void EnsureStarted()
    {
        if (_initialized)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_initialized)
            {
                return;
            }

            StartServerProcess();
            _initialized = true;
        }
    }

    private void StartServerProcess()
    {
        if (!Directory.Exists(_projectPath))
        {
            throw new DirectoryNotFoundException($"Could not locate web project at '{_projectPath}'.");
        }

        var port = GetAvailablePort();
        var address = $"http://127.0.0.1:{port}";
        var args = $"run --project \"{_projectPath}\" --no-build --no-restore -- --urls \"{address}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = _projectPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["MSBuildEnableWorkloadResolver"] = "false";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        _process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _startupOutput.AppendLine(args.Data);
            }
        };
        _process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _startupOutput.AppendLine(args.Data);
            }
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet process for AIUsageTracker.Web.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        WaitForServerReady(address);
        _serverAddress = address;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return port;
    }

    private void WaitForServerReady(string address)
    {
        var port = new Uri(address).Port;
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < TimeSpan.FromSeconds(30))
        {
            if (_process == null || _process.HasExited)
            {
                throw new InvalidOperationException(
                    "AIUsageTracker.Web process exited before becoming available. "
                    + $"Output: {_startupOutput}");
            }

            try
            {
                using var ping = new TcpClient();
                ping.Connect(IPAddress.Loopback, port);
                return;
            }
            catch
            {
                // Intentionally ignore startup race; keep polling.
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException(
            $"AIUsageTracker.Web did not start on {address} within 30s. "
            + $"Output: {_startupOutput}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_process == null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.CloseMainWindow();
                if (!_process.WaitForExit(5000))
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5000);
                }
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
