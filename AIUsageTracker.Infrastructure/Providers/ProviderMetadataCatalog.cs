using System.Reflection;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Infrastructure.Providers;

public static class ProviderMetadataCatalog
{
    private static readonly Lazy<IReadOnlyList<ProviderDefinition>> DefinitionsValue = new(LoadDefinitions);

    public static IReadOnlyList<ProviderDefinition> Definitions => DefinitionsValue.Value;

    public static ProviderDefinition? Find(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        return Definitions.FirstOrDefault(definition => definition.HandlesProviderId(providerId));
    }

    public static bool TryGet(string providerId, out ProviderDefinition definition)
    {
        var found = Find(providerId);
        if (found == null)
        {
            definition = null!;
            return false;
        }

        definition = found;
        return true;
    }

    public static string GetDisplayName(string providerId, string? providerName = null)
    {
        if (TryGet(providerId, out var definition))
        {
            var mapped = definition.ResolveDisplayName(providerId);
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }
        }

        if (!string.IsNullOrWhiteSpace(providerName))
        {
            return providerName;
        }

        return providerId ?? string.Empty;
    }

    public static bool IsAutoIncluded(string providerId)
    {
        return TryGet(providerId, out var definition) && definition.AutoIncludeWhenUnconfigured;
    }

    public static string GetCanonicalProviderId(string providerId)
    {
        if (TryGet(providerId, out var definition))
        {
            return definition.ProviderId;
        }

        return providerId ?? string.Empty;
    }

    public static bool TryCreateDefaultConfig(
        string providerId,
        out ProviderConfig config,
        string? apiKey = null,
        string? authSource = null,
        string? description = null)
    {
        if (!TryGet(providerId, out var definition))
        {
            config = null!;
            return false;
        }

        config = new ProviderConfig
        {
            ProviderId = providerId,
            ApiKey = apiKey ?? string.Empty,
            Type = definition.DefaultConfigType,
            PlanType = definition.PlanType,
            AuthSource = authSource ?? "Unknown",
            Description = description
        };

        return true;
    }

    private static IReadOnlyList<ProviderDefinition> LoadDefinitions()
    {
        var definitions = typeof(ProviderMetadataCatalog).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                typeof(IProviderService).IsAssignableFrom(type))
            .Select(ReadDefinition)
            .OrderBy(definition => definition.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (definitions.Count == 0)
        {
            throw new InvalidOperationException("No provider definitions were discovered.");
        }

        ValidateNoDuplicateProviderIds(definitions);
        ValidateNoDuplicateHandledProviderIds(definitions);

        return definitions;
    }

    private static ProviderDefinition ReadDefinition(Type providerType)
    {
        var staticDefinitionProperty = providerType.GetProperty(
            "StaticDefinition",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (staticDefinitionProperty?.PropertyType != typeof(ProviderDefinition))
        {
            throw new InvalidOperationException(
                $"Provider type '{providerType.FullName}' must expose a public static ProviderDefinition StaticDefinition property.");
        }

        if (staticDefinitionProperty.GetValue(null) is not ProviderDefinition definition)
        {
            throw new InvalidOperationException(
                $"Provider type '{providerType.FullName}' returned a null provider definition.");
        }

        return definition;
    }

    private static void ValidateNoDuplicateProviderIds(IReadOnlyCollection<ProviderDefinition> definitions)
    {
        var duplicateProviderIds = definitions
            .GroupBy(definition => definition.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateProviderIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate provider definitions detected: {string.Join(", ", duplicateProviderIds)}");
        }
    }

    private static void ValidateNoDuplicateHandledProviderIds(IReadOnlyCollection<ProviderDefinition> definitions)
    {
        var duplicateHandledIds = definitions
            .SelectMany(definition => definition.HandledProviderIds.Select(handledId => new
            {
                HandledId = handledId,
                definition.ProviderId
            }))
            .GroupBy(item => item.HandledId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.ProviderId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateHandledIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate handled provider ids detected: {string.Join(", ", duplicateHandledIds)}");
        }
    }
}
