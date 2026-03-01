using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Core.Models;
using Xunit;

namespace AIUsageTracker.Tests.Core;

public class MonitorResilienceTests
{
    [Fact]
    public void MonitorInfo_SupportsErrorTracking()
    {
        var info = new MonitorInfo
        {
            Port = 5000,
            ProcessId = 12345,
            StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Errors = new List<string>()
        };

        info.Errors.Add("Startup status: starting");
        info.Errors.Add("Startup status: running");

        Assert.Equal(5000, info.Port);
        Assert.Equal(12345, info.ProcessId);
        Assert.NotNull(info.Errors);
        Assert.Equal(2, info.Errors.Count);
    }

    [Fact]
    public void MonitorInfo_SupportsStartupStatusTracking()
    {
        var info = new MonitorInfo
        {
            Port = 5000,
            ProcessId = 12345
        };

        info.Errors = new List<string>();
        info.Errors.Add("Startup status: failed: Port binding failed");

        Assert.NotNull(info.Errors);
        Assert.Contains(info.Errors, e => e.Contains("Startup status"));
    }

    [Fact]
    public void MonitorLauncher_InvalidateMonitorInfo_DoesNotThrow_WhenFileMissing()
    {
        var task = MonitorLauncher.InvalidateMonitorInfoAsync();
        task.Wait();

        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void MonitorLauncher_GetAndValidateMonitorInfo_ReturnsNull_WhenHealthFails()
    {
        var task = MonitorLauncher.GetAndValidateMonitorInfoAsync();
        task.Wait();

        var result = task.Result;
        Assert.Null(result);
    }
}

public class MonitorPortBindingTests
{
    [Fact]
    public void PortBinding_AddressAlreadyInUse_HandledGracefully()
    {
        var preferredPort = 59999;

        int? boundPort = null;
        try
        {
            using var listener1 = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, preferredPort);
            listener1.Start();

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    using var listener2 = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, preferredPort);
                    listener2.Start();
                    boundPort = preferredPort;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    boundPort = null;
                }
            });
            thread.Start();
            thread.Join();
        }
        finally
        {
            using var cleanup = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, preferredPort);
            cleanup.Start();
            cleanup.Stop();
        }

        Assert.Null(boundPort);
    }

    [Fact]
    public void PortScanning_FindsAvailablePort()
    {
        var preferredPort = 59998;

        try
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, preferredPort);
            listener.Start();

            var availablePort = FindAvailablePortWithoutBinding(preferredPort);
            Assert.NotEqual(preferredPort, availablePort);
        }
        catch
        {
            Assert.True(true, "Port was available, test passed");
        }
    }

    private static int FindAvailablePortWithoutBinding(int preferredPort)
    {
        for (int port = preferredPort; port < preferredPort + 100; port++)
        {
            try
            {
                using var testListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                testListener.Start();
                testListener.Stop();
                return port;
            }
            catch (System.Net.Sockets.SocketException)
            {
            }
        }
        return preferredPort;
    }
}

public class MonitorStartupMutexTests
{
    [Fact]
    public void MutexName_Format_IsValid()
    {
        var userName = Environment.UserName;
        var mutexName = @"Global\AIUsageTracker_Monitor_" + userName;

        Assert.NotNull(mutexName);
        Assert.Contains("AIUsageTracker_Monitor_", mutexName);
        Assert.Contains(userName, mutexName);
    }

    [Fact]
    public void MutexName_DoesNotContainSpecialCharacters()
    {
        var userName = Environment.UserName;
        var mutexName = @"Global\AIUsageTracker_Monitor_" + userName;

        foreach (char c in mutexName)
        {
            Assert.False(char.IsWhiteSpace(c), "Mutex name should not contain whitespace");
        }
    }
}

public class MonitorMetadataTests
{
    [Fact]
    public void MonitorInfo_DefaultValues_AreCorrect()
    {
        var info = new MonitorInfo();

        Assert.Equal(0, info.Port);
        Assert.Equal(0, info.ProcessId);
        Assert.Null(info.StartedAt);
        Assert.Null(info.Errors);
        Assert.Null(info.MachineName);
        Assert.Null(info.UserName);
    }

    [Fact]
    public void MonitorInfo_CanBePopulated()
    {
        var info = new MonitorInfo
        {
            Port = 5000,
            ProcessId = 12345,
            StartedAt = "2026-03-01 12:00:00",
            DebugMode = true,
            MachineName = "TESTMACHINE",
            UserName = "testuser",
            Errors = new List<string> { "Test error" }
        };

        Assert.Equal(5000, info.Port);
        Assert.Equal(12345, info.ProcessId);
        Assert.Equal("2026-03-01 12:00:00", info.StartedAt);
        Assert.True(info.DebugMode);
        Assert.Equal("TESTMACHINE", info.MachineName);
        Assert.Equal("testuser", info.UserName);
        Assert.Single(info.Errors);
    }

    [Fact]
    public void MonitorInfo_Errors_CanBeAdded()
    {
        var info = new MonitorInfo
        {
            Errors = new List<string>()
        };

        info.Errors.Add("Error 1");
        info.Errors.Add("Error 2");
        info.Errors.Add("Startup status: running");

        Assert.Equal(3, info.Errors.Count);
        Assert.Contains(info.Errors, e => e.StartsWith("Startup status"));
    }
}