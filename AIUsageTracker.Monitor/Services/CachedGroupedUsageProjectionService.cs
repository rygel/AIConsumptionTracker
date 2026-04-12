// <copyright file="CachedGroupedUsageProjectionService.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.Monitor.Services;

public sealed class CachedGroupedUsageProjectionService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IUsageDatabase _database;
    private readonly IConfigService _configService;
    private readonly object _lock = new();
    private AgentGroupedUsageSnapshot? _cachedSnapshot;
    private string? _cachedETag;
    private DateTime _cacheTimestamp = DateTime.MinValue;

    public CachedGroupedUsageProjectionService(IUsageDatabase database, IConfigService configService)
    {
        this._database = database;
        this._configService = configService;
    }

    public async Task<AgentGroupedUsageSnapshot> GetGroupedUsageAsync()
    {
        var entry = await this.GetGroupedUsageWithMetadataAsync().ConfigureAwait(false);
        return entry.Snapshot;
    }

    internal async Task<GroupedUsageCacheEntry> GetGroupedUsageWithMetadataAsync()
    {
        lock (this._lock)
        {
            if (this._cachedSnapshot != null &&
                this._cachedETag != null &&
                DateTime.UtcNow - this._cacheTimestamp < CacheDuration)
            {
                return new GroupedUsageCacheEntry(this._cachedSnapshot, this._cachedETag);
            }
        }

        var allUsage = await this._database.GetLatestHistoryAsync().ConfigureAwait(false);
        var activeConfigs = await this._configService.GetConfigsAsync().ConfigureAwait(false);
        var activeIds = ProviderMetadataCatalog.ExpandAcceptedUsageProviderIds(
            activeConfigs.Select(c => c.ProviderId));

        // Providers with no API key that are StandardApiKey mode produce only "API Key missing"
        // records. Exclude their canonical IDs from the snapshot so they don't appear in the
        // main window. (They remain in Settings where the user can configure a key.)
        var unconfiguredStandardApiKeyIds = activeConfigs
            .Where(c => string.IsNullOrEmpty(c.ApiKey) &&
                        ProviderMetadataCatalog.Find(c.ProviderId)?.SettingsMode == ProviderSettingsMode.StandardApiKey)
            .Select(c => ProviderMetadataCatalog.GetCanonicalProviderId(c.ProviderId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usage = allUsage
            .Where(u => activeIds.Contains(u.ProviderId ?? string.Empty) &&
                        !unconfiguredStandardApiKeyIds.Contains(
                            ProviderMetadataCatalog.GetCanonicalProviderId(u.ProviderId ?? string.Empty)))
            .ToList();
        var snapshot = GroupedUsageProjectionService.Build(usage);
        var eTag = CreateUsageETag(usage);

        lock (this._lock)
        {
            this._cachedSnapshot = snapshot;
            this._cachedETag = eTag;
            this._cacheTimestamp = DateTime.UtcNow;
        }

        return new GroupedUsageCacheEntry(snapshot, eTag);
    }

    public void Invalidate()
    {
        lock (this._lock)
        {
            this._cachedSnapshot = null;
            this._cachedETag = null;
            this._cacheTimestamp = DateTime.MinValue;
        }
    }

    private static string CreateUsageETag(IReadOnlyList<ProviderUsage> usages)
    {
        var payload = usages.Select(usage => new
        {
            usage.ProviderId,
            usage.CardId,
            usage.GroupId,
            usage.ParentProviderId,
            usage.WindowKind,
            usage.ModelName,
            usage.Name,
            usage.IsAvailable,
            usage.RequestsUsed,
            usage.RequestsAvailable,
            usage.UsedPercent,
            usage.HttpStatus,
            usage.Description,
            usage.FetchedAt,
            usage.NextResetTime,
        });

        var json = JsonSerializer.Serialize(payload);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hashBytes)}\"";
    }

    internal sealed record GroupedUsageCacheEntry(AgentGroupedUsageSnapshot Snapshot, string ETag);
}
