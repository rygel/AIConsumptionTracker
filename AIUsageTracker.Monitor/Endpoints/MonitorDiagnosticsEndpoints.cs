// <copyright file="MonitorDiagnosticsEndpoints.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Monitor.Endpoints
{
    using AIUsageTracker.Monitor.Services;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    internal static class MonitorDiagnosticsEndpoints
    {
        public static void Map(
            WebApplication app,
            bool isDebugMode,
            int port,
            string agentVersion,
            string apiContractVersion,
            string[] args)
        {
            app.MapGet(MonitorApiRoutes.Health, (ProviderRefreshService refreshService, ILogger<Program> logger) =>
            {
                if (isDebugMode)
                {
                    logger.LogDebug("GET {Route}", MonitorApiRoutes.Health);
                }

                var refreshTelemetry = refreshService.GetRefreshTelemetrySnapshot();
                var failingProviders = refreshTelemetry.ProviderDiagnostics
                    .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.LastRefreshError))
                    .Select(diagnostic => diagnostic.ProviderId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(providerId => providerId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var providersInBackoff = refreshTelemetry.ProviderDiagnostics.Count(diagnostic => diagnostic.IsCircuitOpen);
                var refreshStatus = refreshTelemetry.LastRefreshAttemptUtc == null
                    ? "idle"
                    : (providersInBackoff > 0 || failingProviders.Length > 0 || !string.IsNullOrWhiteSpace(refreshTelemetry.LastError)
                        ? "degraded"
                        : "healthy");

                return Results.Ok(new
                {
                    status = "healthy",
                    serviceHealth = refreshStatus,
                    timestamp = DateTime.UtcNow,
                    port,
                    processId = Environment.ProcessId,
                    agentVersion,
                    apiContractVersion,
                    refreshHealth = new
                    {
                        status = refreshStatus,
                        lastRefreshAttemptUtc = refreshTelemetry.LastRefreshAttemptUtc,
                        lastRefreshCompletedUtc = refreshTelemetry.LastRefreshCompletedUtc,
                        lastSuccessfulRefreshUtc = refreshTelemetry.LastSuccessfulRefreshUtc,
                        lastError = refreshTelemetry.LastError,
                        providersInBackoff,
                        failingProviders,
                    },
                });
            });

            app.MapGet(MonitorApiRoutes.Diagnostics, (EndpointDataSource endpointDataSource, ProviderRefreshService refreshService, IMonitorJobScheduler scheduler, IProviderUsageProcessingPipeline usageProcessingPipeline, ILogger<Program> logger) =>
            {
                if (isDebugMode)
                {
                    logger.LogDebug("GET {Route}", MonitorApiRoutes.Diagnostics);
                }

                var snapshot = MonitorDiagnosticsSnapshotFactory.Create(
                    endpointDataSource,
                    port,
                    args,
                    refreshService.GetRefreshTelemetrySnapshot(),
                    scheduler.GetSnapshot(),
                    usageProcessingPipeline.GetSnapshot());

                return Results.Ok(snapshot);
            });
        }
    }
}
