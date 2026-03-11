// <copyright file="AuthDiagnosticsSnapshotBuilder.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Monitor.Services;

internal static class AuthDiagnosticsSnapshotBuilder
{
    public static AuthDiagnosticsSnapshot Build(ProviderConfig config, DateTimeOffset nowUtc)
    {
        var authSource = string.IsNullOrWhiteSpace(config.AuthSource) ? "none" : config.AuthSource;
        var fallbackPathUsed = GetFallbackPath(authSource);

        return new AuthDiagnosticsSnapshot(
            ProviderId: config.ProviderId,
            Configured: !string.IsNullOrWhiteSpace(config.ApiKey),
            AuthSource: authSource,
            FallbackPathUsed: fallbackPathUsed,
            TokenAgeBucket: GetTokenAgeBucket(authSource, fallbackPathUsed, nowUtc),
            HasUserIdentity: HasUserIdentity(config.Description, authSource));
    }

    private static string GetFallbackPath(string authSource)
    {
        var separatorIndex = authSource.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex == authSource.Length - 1)
        {
            return "n/a";
        }

        var candidate = authSource[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "n/a";
        }

        return candidate.Contains('\\') || candidate.Contains('/')
            ? candidate
            : "n/a";
    }

    private static string GetTokenAgeBucket(string authSource, string fallbackPathUsed, DateTimeOffset nowUtc)
    {
        if (authSource.StartsWith("Env:", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime-env";
        }

        if (string.Equals(fallbackPathUsed, "n/a", StringComparison.Ordinal))
        {
            return "unknown";
        }

        try
        {
            if (!File.Exists(fallbackPathUsed))
            {
                return "missing";
            }

            var lastWriteUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(fallbackPathUsed), TimeSpan.Zero);
            var age = nowUtc - lastWriteUtc;
            if (age <= TimeSpan.FromHours(1))
            {
                return "lt-1h";
            }

            if (age <= TimeSpan.FromHours(24))
            {
                return "lt-24h";
            }

            if (age <= TimeSpan.FromDays(7))
            {
                return "lt-7d";
            }

            return "gte-7d";
        }
        catch
        {
            return "unknown";
        }
    }

    private static bool HasUserIdentity(string? description, string authSource)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            if (description.Contains('@', StringComparison.Ordinal) ||
                description.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("account", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return authSource.Contains("github", StringComparison.OrdinalIgnoreCase) ||
               authSource.Contains("copilot", StringComparison.OrdinalIgnoreCase);
    }
}
