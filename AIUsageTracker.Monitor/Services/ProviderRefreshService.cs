using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.Services;
using AIUsageTracker.Infrastructure.Configuration;
using AIUsageTracker.Infrastructure.Providers;
using AIUsageTracker.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AIUsageTracker.Monitor.Services;

public class ProviderRefreshService : BackgroundService
{
    private readonly ILogger<ProviderRefreshService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IUsageDatabase _database;
    private readonly INotificationService _notificationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfigService _configService;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);
    private static bool _debugMode = false;
    private ProviderManager? _providerManager;
    private long _refreshCount;
    private long _refreshFailureCount;
    private long _refreshTotalLatencyMs;
    private long _lastRefreshLatencyMs;
    private readonly object _telemetryLock = new();
    private DateTime? _lastRefreshCompletedUtc;
    private string? _lastRefreshError;

    public static void SetDebugMode(bool debug)
    {
        _debugMode = debug;
    }

    public ProviderRefreshService(
        ILogger<ProviderRefreshService> logger,
        ILoggerFactory loggerFactory,
        IUsageDatabase database,
        INotificationService notificationService,
        IHttpClientFactory httpClientFactory,
        IConfigService configService)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _database = database;
        _notificationService = notificationService;
        _httpClientFactory = httpClientFactory;
        _configService = configService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting...");

        _notificationService.Initialize();
        InitializeProviders();

        var isEmpty = await _database.IsHistoryEmptyAsync();
        if (isEmpty)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("First-time startup: scanning for keys and seeding database.");
                    await _configService.ScanForKeysAsync();
                    await TriggerRefreshAsync(forceAll: true);
                    _logger.LogInformation("First-time data seeding complete.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during first-time data seeding.");
                }
            }, stoppingToken);
        }
        else
        {
            _logger.LogInformation("Startup: serving cached data from database (next refresh in {Minutes}m).", _refreshInterval.TotalMinutes);
            
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Startup: running targeted refresh for system providers...");
                    await TriggerRefreshAsync(
                        forceAll: true,
                        includeProviderIds: new[] { "antigravity" });
                    _logger.LogDebug("Startup: targeted refresh complete.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Startup targeted refresh failed");
                }
            }, stoppingToken);
        }


        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Next refresh in {Minutes} minutes...", _refreshInterval.TotalMinutes);
                await Task.Delay(_refreshInterval, stoppingToken);
                await TriggerRefreshAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled refresh: {Message}", ex.Message);
            }
        }

        _logger.LogInformation("Stopping...");
    }

    private void InitializeProviders()
    {
        _logger.LogDebug("Initializing providers...");

        var httpClient = _httpClientFactory.CreateClient();
        var configLoader = new JsonConfigLoader(
            _loggerFactory.CreateLogger<JsonConfigLoader>(),
            _loggerFactory.CreateLogger<TokenDiscoveryService>());

        var gitHubAuthService = new GitHubAuthService(
            httpClient,
            _loggerFactory.CreateLogger<GitHubAuthService>());

        var providers = new IProviderService[]
        {
            new AntigravityProvider(httpClient, _loggerFactory.CreateLogger<AntigravityProvider>()),
            new DeepSeekProvider(httpClient, _loggerFactory.CreateLogger<DeepSeekProvider>()),
            new GeminiProvider(httpClient, _loggerFactory.CreateLogger<GeminiProvider>()),
            new MistralProvider(httpClient, _loggerFactory.CreateLogger<MistralProvider>()),
            new XiaomiProvider(httpClient, _loggerFactory.CreateLogger<XiaomiProvider>()),
            new GitHubCopilotProvider(
                httpClient,
                _loggerFactory.CreateLogger<GitHubCopilotProvider>(),
                gitHubAuthService),
            new ZaiProvider(httpClient, _loggerFactory.CreateLogger<ZaiProvider>()),
            new KimiProvider(httpClient, _loggerFactory.CreateLogger<KimiProvider>()),
            new MinimaxProvider(httpClient, _loggerFactory.CreateLogger<MinimaxProvider>()),
            new OpenCodeProvider(httpClient, _loggerFactory.CreateLogger<OpenCodeProvider>()),
            new CodexProvider(httpClient, _loggerFactory.CreateLogger<CodexProvider>()),
            new AnthropicProvider(_loggerFactory.CreateLogger<AnthropicProvider>()),
            new OpenCodeZenProvider(_loggerFactory.CreateLogger<OpenCodeZenProvider>()),
            new OpenRouterProvider(httpClient, _loggerFactory.CreateLogger<OpenRouterProvider>()),
            new SyntheticProvider(httpClient, _loggerFactory.CreateLogger<SyntheticProvider>()),
            new ClaudeCodeProvider(_loggerFactory.CreateLogger<ClaudeCodeProvider>(), httpClient)
        };

        _providerManager = new ProviderManager(
            providers,
            configLoader,
            _loggerFactory.CreateLogger<ProviderManager>());
    }

    public virtual async Task TriggerRefreshAsync(bool forceAll = false, IReadOnlyCollection<string>? includeProviderIds = null)
    {
        if (_providerManager == null)
        {
            _logger.LogWarning("Provider manager not initialized.");
            return;
        }

        if (!_refreshSemaphore.Wait(0))
        {
            _logger.LogInformation("Refresh already in progress, skipping.");
            return;
        }

        var refreshStopwatch = Stopwatch.StartNew();
        var refreshSucceeded = false;
        string? refreshError = null;

        try
        {
            var configs = await _configService.GetConfigsAsync();
            var systemProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "antigravity", "claude-code", "mistral", "opencode-zen" };
            
            var activeConfigs = configs.Where(c =>
                forceAll ||
                systemProviders.Contains(c.ProviderId) ||
                c.ProviderId.StartsWith("antigravity.", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(c.ApiKey)).ToList();

            if (includeProviderIds != null && includeProviderIds.Count > 0)
            {
                var filter = new HashSet<string>(includeProviderIds, StringComparer.OrdinalIgnoreCase);
                activeConfigs = activeConfigs.Where(c => filter.Contains(c.ProviderId)).ToList();
            }

            if (activeConfigs.Count > 0)
            {
                _logger.LogInformation("Refreshing {Count} providers...", activeConfigs.Count);
                
                var activeProviderIds = activeConfigs.Select(c => c.ProviderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                var usages = await _providerManager.GetAllUsageAsync(
                    forceRefresh: forceAll,
                    overrideConfigs: activeConfigs);

                var validatedUsages = usages.Where(u => u != null).ToList();

                var filteredUsages = validatedUsages.Where(u => 
                    IsUsageForAnyActiveProvider(activeProviderIds, u.ProviderId) &&
                    !(u.RequestsAvailable == 0 && u.RequestsUsed == 0 && u.RequestsPercentage == 0 && !u.IsAvailable)
                ).ToList();

                _logger.LogDebug("Provider query results:");
                foreach (var usage in filteredUsages)
                {
                    var status = usage.IsAvailable ? "OK" : "FAILED";
                    var msg = usage.IsAvailable
                        ? $"{usage.RequestsPercentage:F1}% used"
                        : usage.Description;
                    _logger.LogDebug("  {ProviderId}: [{Status}] {Message}", usage.ProviderId, status, msg);
                }

                foreach (var usage in filteredUsages)
                {
                    if (!activeProviderIds.Contains(usage.ProviderId))
                    {
                        if (!activeProviderIds.Contains(usage.ProviderId))
                        {
                            _logger.LogInformation("Auto-registering dynamic provider: {ProviderId}", usage.ProviderId);
                        }
                        
                        var dynamicConfig = new ProviderConfig
                        {
                            ProviderId = usage.ProviderId,
                            Type = usage.IsQuotaBased ? "quota-based" : "pay-as-you-go",
                            AuthSource = usage.AuthSource,
                            ApiKey = "dynamic"
                        };
                        
                        await _database.StoreProviderAsync(dynamicConfig, usage.ProviderName);
                        activeProviderIds.Add(usage.ProviderId);
                    }
                }

                await _database.StoreHistoryAsync(filteredUsages);
                _logger.LogDebug("Stored {Count} provider histories", filteredUsages.Count);

                foreach (var usage in filteredUsages.Where(u => !string.IsNullOrEmpty(u.RawJson)))
                {
                    await _database.StoreRawSnapshotAsync(usage.ProviderId, usage.RawJson!, usage.HttpStatus);
                }

                await DetectResetEventsAsync(filteredUsages);
                var prefs = await _configService.GetPreferencesAsync();
                CheckUsageAlerts(filteredUsages, prefs, configs);

                _logger.LogInformation("Done: {Count} records", filteredUsages.Count);
            }
            else
            {
                _logger.LogDebug("No refreshable providers currently available.");
            }

            await _database.CleanupOldSnapshotsAsync();
            await _database.OptimizeAsync();
            refreshSucceeded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh failed: {Message}", ex.Message);
            Program.ReportError($"Refresh failed: {ex.Message}", _logger);
            refreshError = ex.Message;
        }
        finally
        {
            refreshStopwatch.Stop();
            RecordRefreshTelemetry(refreshStopwatch.Elapsed, refreshSucceeded, refreshError);
            _refreshSemaphore.Release();
        }
    }

    public RefreshTelemetrySnapshot GetRefreshTelemetrySnapshot()
    {
        var refreshCount = Interlocked.Read(ref _refreshCount);
        var refreshFailureCount = Interlocked.Read(ref _refreshFailureCount);
        var refreshTotalLatencyMs = Interlocked.Read(ref _refreshTotalLatencyMs);
        var lastRefreshLatencyMs = Interlocked.Read(ref _lastRefreshLatencyMs);

        DateTime? lastRefreshCompletedUtc;
        string? lastRefreshError;
        lock (_telemetryLock)
        {
            lastRefreshCompletedUtc = _lastRefreshCompletedUtc;
            lastRefreshError = _lastRefreshError;
        }

        var refreshSuccessCount = Math.Max(0, refreshCount - refreshFailureCount);
        var averageLatencyMs = refreshCount == 0 ? 0 : refreshTotalLatencyMs / (double)refreshCount;
        var errorRatePercent = refreshCount == 0 ? 0 : (refreshFailureCount / (double)refreshCount) * 100.0;

        return new RefreshTelemetrySnapshot
        {
            RefreshCount = refreshCount,
            RefreshSuccessCount = refreshSuccessCount,
            RefreshFailureCount = refreshFailureCount,
            ErrorRatePercent = errorRatePercent,
            AverageLatencyMs = averageLatencyMs,
            LastLatencyMs = lastRefreshLatencyMs,
            LastRefreshCompletedUtc = lastRefreshCompletedUtc,
            LastError = lastRefreshError
        };
    }

    private void RecordRefreshTelemetry(TimeSpan duration, bool success, string? errorMessage)
    {
        var latencyMs = (long)Math.Max(0, duration.TotalMilliseconds);
        Interlocked.Increment(ref _refreshCount);
        Interlocked.Add(ref _refreshTotalLatencyMs, latencyMs);
        Interlocked.Exchange(ref _lastRefreshLatencyMs, latencyMs);

        if (!success)
        {
            Interlocked.Increment(ref _refreshFailureCount);
        }

        lock (_telemetryLock)
        {
            _lastRefreshCompletedUtc = DateTime.UtcNow;
            _lastRefreshError = success ? null : errorMessage;
        }
    }

    private static bool IsUsageForAnyActiveProvider(HashSet<string> activeProviderIds, string usageProviderId)
    {
        if (activeProviderIds.Contains(usageProviderId)) return true;
        return activeProviderIds.Any(providerId => IsUsageForProvider(providerId, usageProviderId));
    }

    internal static bool IsUsageForProvider(string configProviderId, string usageProviderId)
    {
        if (string.Equals(configProviderId, usageProviderId, StringComparison.OrdinalIgnoreCase)) return true;
        if (usageProviderId.StartsWith(configProviderId + ".", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsDynamicChildOfAnyActiveProvider(HashSet<string> activeProviderIds, string usageProviderId)
    {
        return activeProviderIds.Any(id => usageProviderId.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSuccessfulUsage(ProviderUsage usage)
    {
        if (!usage.IsAvailable) return false;
        if (usage.HttpStatus >= 400) return false;
        return true;
    }

    private async Task DetectResetEventsAsync(List<ProviderUsage> currentUsages)
    {
        try
        {
            foreach (var usage in currentUsages.Where(u => u.IsAvailable))
            {
                var history = await _database.GetHistoryByProviderAsync(usage.ProviderId, 2);
                if (history.Count < 2) continue;

                var latest = history[0];
                var previous = history[1];

                if (UsageMath.IsQuotaReset(previous.RequestsPercentage, latest.RequestsPercentage))
                {
                    _logger.LogInformation("Reset detected for {ProviderId}", usage.ProviderId);
                    await _database.StoreResetEventAsync(usage.ProviderId, usage.ProviderName, previous.RequestsPercentage, latest.RequestsPercentage, "Automatic");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during reset detection");
        }
    }

    internal void CheckUsageAlerts(List<ProviderUsage> usages, AppPreferences prefs, List<ProviderConfig> configs)
    {
        if (!prefs.EnableNotifications) return;

        foreach (var usage in usages.Where(u => u.IsAvailable))
        {
            var config = configs.FirstOrDefault(c => IsUsageForProvider(c.ProviderId, usage.ProviderId));
            if (config != null && !config.EnableNotifications) continue;

            if (usage.RequestsPercentage >= prefs.NotificationThreshold)
            {
                _notificationService.ShowNotification(
                    usage.ProviderName,
                    $"Usage reached {usage.RequestsPercentage:F1}%",
                    "openDashboard",
                    usage.ProviderId);
            }
        }
    }
}

public record RefreshTelemetrySnapshot
{
    public long RefreshCount { get; init; }
    public long RefreshSuccessCount { get; init; }
    public long RefreshFailureCount { get; init; }
    public double ErrorRatePercent { get; init; }
    public double AverageLatencyMs { get; init; }
    public long LastLatencyMs { get; init; }
    public DateTime? LastRefreshCompletedUtc { get; init; }
    public string? LastError { get; init; }
}
