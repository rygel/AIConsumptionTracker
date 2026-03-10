// <copyright file="MonitorUsageEndpoints.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Infrastructure.Providers;
using AIUsageTracker.Monitor.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.Monitor.Endpoints;

internal static class MonitorUsageEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(MonitorApiRoutes.Usage, async (UsageDatabase db, IConfigService configService, ILogger<Program> logger) =>
        {
            var usage = await db.GetLatestHistoryAsync().ConfigureAwait(false);

            var configs = await configService.GetConfigsAsync().ConfigureAwait(false);
            usage = usage
                .Where(u => !ProviderMetadataCatalog.ShouldSuppressUsageProviderId(configs, u.ProviderId))
                .ToList();

            logger.LogDebug(
                "GET /api/usage returning {Count} providers: {Providers}",
                usage.Count,
                string.Join(", ", usage.Select(u => u.ProviderId)));

            return Results.Ok(usage);
        });

        app.MapGet(MonitorApiRoutes.UsageByProvider, async (string providerId, UsageDatabase db, ILogger<Program> logger) =>
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return Results.BadRequest(new { message = "providerId is required." });
            }

            logger.LogDebug("GET {Route}: {ProviderId}", MonitorApiRoutes.UsageByProvider, providerId);
            var usage = await db.GetHistoryByProviderAsync(providerId, 1).ConfigureAwait(false);
            var result = usage.FirstOrDefault();
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        app.MapPost(MonitorApiRoutes.Refresh, ([FromServices] ProviderRefreshService refreshService, ILogger<Program> logger, [FromQuery] bool forceAll = false, [FromQuery] string? providerIds = null) =>
        {
            var includeProviderIds = ParseProviderIds(providerIds);
            logger.LogDebug(
                "POST {Route} forceAll={ForceAll} includeProviderCount={IncludeProviderCount}",
                MonitorApiRoutes.Refresh,
                forceAll,
                includeProviderIds?.Count ?? 0);
            var queued = refreshService.QueueForceRefresh(
                forceAll: forceAll,
                includeProviderIds: includeProviderIds);
            return Results.Ok(new
            {
                message = queued ? "Refresh queued" : "Refresh already queued",
                queued,
                forceAll,
                includeProviderCount = includeProviderIds?.Count ?? 0,
            });
        });

        app.MapPost(MonitorApiRoutes.NotificationTest, ([FromServices] INotificationService notificationService, ILogger<Program> logger) =>
        {
            logger.LogDebug("POST {Route}", MonitorApiRoutes.NotificationTest);
            notificationService.ShowNotification(
                "AI Usage Tracker",
                "This is a test notification from Slim Settings.",
                "openSettings",
                "notifications");
            return Results.Ok(new { message = "Test notification sent" });
        });
    }

    private static IReadOnlyCollection<string>? ParseProviderIds(string? providerIds)
    {
        if (string.IsNullOrWhiteSpace(providerIds))
        {
            return null;
        }

        var parsed = providerIds
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return parsed.Length == 0 ? null : parsed;
    }
}
