// <copyright file="ConfigPathCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Interfaces;

namespace AIUsageTracker.Core.Paths;

public static class ConfigPathCatalog
{
    public static IReadOnlyList<ConfigPathEntry> GetConfigEntries(IAppPathProvider pathProvider)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        var appDataRoot = pathProvider.GetAppDataRoot();
        var legacyAuthPath = string.IsNullOrWhiteSpace(appDataRoot)
            ? null
            : Path.Combine(appDataRoot, "auth.json");

        var entries = new[]
        {
            new ConfigPathEntry(pathProvider.GetAuthFilePath(), ConfigPathKind.Auth),
            new ConfigPathEntry(pathProvider.GetProviderConfigFilePath(), ConfigPathKind.Provider),
            new ConfigPathEntry(legacyAuthPath, ConfigPathKind.Auth),
        };

        var distinctEntries = new List<ConfigPathEntry>(entries.Length);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                continue;
            }

            if (seenPaths.Add(entry.Path))
            {
                distinctEntries.Add(entry);
            }
        }

        return distinctEntries;
    }
}
