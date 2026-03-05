using System.Text.Json;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Infrastructure.Providers;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.Monitor.Services;

public class UsageDatabase : IUsageDatabase
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly ILogger<UsageDatabase> _logger;
    private readonly IAppPathProvider _pathProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public UsageDatabase(ILogger<UsageDatabase> logger, IAppPathProvider pathProvider)
    {
        _logger = logger;
        _pathProvider = pathProvider;
        _dbPath = _pathProvider.GetDatabasePath();
        
        var dbDir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _logger.LogInformation("Database path: {DbPath}", _dbPath);
        
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 15
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await Task.Run(() => RunMigrations());
    }

    private void RunMigrations()
    {
        var migrationService = new DatabaseMigrationService(_dbPath, 
            LoggerFactory.Create(builder => builder.AddProvider(new LoggerProvider(_logger))).CreateLogger<DatabaseMigrationService>());
        migrationService.RunMigrations();
    }

    private class LoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;
        public LoggerProvider(ILogger logger) => _logger = logger;
        public ILogger CreateLogger(string categoryName) => _logger;
        public void Dispose() { }
    }

    public async Task StoreProviderAsync(ProviderConfig config, string? friendlyName = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO providers (
                    provider_id, provider_name, auth_source, account_name, created_at, updated_at, is_active, config_json
                ) VALUES (
                    @ProviderId, @ProviderName, @AuthSource, @AccountName, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 1, @ConfigJson
                )
                ON CONFLICT(provider_id) DO UPDATE SET
                    provider_name = excluded.provider_name,
                    auth_source = excluded.auth_source,
                    account_name = excluded.account_name,
                    updated_at = CURRENT_TIMESTAMP,
                    config_json = excluded.config_json,
                    is_active = 1";

            await connection.ExecuteAsync(sql, new
            {
                ProviderId = config.ProviderId,
                ProviderName = friendlyName ?? config.ProviderId,
                AuthSource = config.AuthSource,
                AccountName = config.AccountName,
                ConfigJson = JsonSerializer.Serialize(config)
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StoreHistoryAsync(ProviderUsage usage)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO provider_history (
                    provider_id, is_available, status_message, next_reset_time, 
                    requests_used, requests_available, requests_percentage, response_latency_ms, fetched_at, details_json
                ) VALUES (
                    @ProviderId, @IsAvailable, @StatusMessage, @NextResetTime, 
                    @RequestsUsed, @RequestsAvailable, @RequestsPercentage, @ResponseLatencyMs, CURRENT_TIMESTAMP, @DetailsJson
                )";

            await connection.ExecuteAsync(sql, new
            {
                ProviderId = usage.ProviderId,
                IsAvailable = usage.IsAvailable ? 1 : 0,
                StatusMessage = usage.Description ?? string.Empty,
                NextResetTime = usage.NextResetTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                RequestsUsed = usage.RequestsUsed,
                RequestsAvailable = usage.RequestsAvailable,
                RequestsPercentage = usage.RequestsPercentage,
                ResponseLatencyMs = usage.ResponseLatencyMs,
                DetailsJson = usage.Details != null ? JsonSerializer.Serialize(usage.Details) : null
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<ProviderUsage>> GetLatestHistoryAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT h.*, p.provider_name as ProviderName 
            FROM provider_history h
            JOIN providers p ON h.provider_id = p.provider_id
            WHERE h.id IN (SELECT MAX(id) FROM provider_history GROUP BY provider_id)
            AND p.is_active = 1";

        var results = await connection.QueryAsync<dynamic>(sql);
        return results.Select(MapToProviderUsage).ToList();
    }

    public async Task<List<ProviderUsage>> GetHistoryAsync(int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT h.*, p.provider_name as ProviderName 
            FROM provider_history h
            JOIN providers p ON h.provider_id = p.provider_id
            ORDER BY h.fetched_at DESC LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new { Limit = limit });
        return results.Select(MapToProviderUsage).ToList();
    }

    public async Task<List<ProviderUsage>> GetHistoryByProviderAsync(string providerId, int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT h.*, p.provider_name as ProviderName 
            FROM provider_history h
            JOIN providers p ON h.provider_id = p.provider_id
            WHERE h.provider_id = @ProviderId
            ORDER BY h.fetched_at DESC LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new { ProviderId = providerId, Limit = limit });
        return results.Select(MapToProviderUsage).ToList();
    }

    public async Task<List<ProviderUsage>> GetResetEventsAsync(string providerId, int limit = 50)
    {
        // For reset events, we just return them as ProviderUsage objects for now or we could define a specific model
        // The interface says Task<List<ProviderUsage>> GetResetEventsAsync
        // But the schema has a reset_events table.
        return new List<ProviderUsage>();
    }

    private ProviderUsage MapToProviderUsage(dynamic row)
    {
        var usage = new ProviderUsage
        {
            ProviderId = row.provider_id,
            ProviderName = row.ProviderName,
            IsAvailable = row.is_available == 1,
            Description = row.status_message,
            RequestsUsed = (double)row.requests_used,
            RequestsAvailable = (double)row.requests_available,
            RequestsPercentage = (double)row.requests_percentage,
            ResponseLatencyMs = (double)row.response_latency_ms,
            FetchedAt = DateTime.Parse(row.fetched_at)
        };

        if (row.next_reset_time != null)
        {
            usage.NextResetTime = DateTime.Parse(row.next_reset_time);
        }

        if (row.details_json != null)
        {
            usage.Details = JsonSerializer.Deserialize<List<ProviderUsageDetail>>(row.details_json);
        }

        return usage;
    }

    public async Task ClearHistoryAsync(string? providerId = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            if (string.IsNullOrEmpty(providerId))
            {
                await connection.ExecuteAsync("DELETE FROM provider_history");
            }
            else
            {
                await connection.ExecuteAsync("DELETE FROM provider_history WHERE provider_id = @ProviderId", new { ProviderId = providerId });
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetProviderActiveAsync(string providerId, bool isActive)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync("UPDATE providers SET is_active = @IsActive, updated_at = CURRENT_TIMESTAMP WHERE provider_id = @ProviderId", 
                new { ProviderId = providerId, IsActive = isActive ? 1 : 0 });
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
 Applied fuzzy match at line 1-585.