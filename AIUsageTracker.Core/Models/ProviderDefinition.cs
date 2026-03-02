namespace AIUsageTracker.Core.Models;

public sealed class ProviderDefinition
{
    public string ProviderId { get; }
    public string DisplayName { get; }
    public PlanType PlanType { get; }
    public bool IsQuotaBased { get; }
    public string DefaultConfigType { get; }
    public string? LogoKey { get; }
    public bool AutoIncludeWhenUnconfigured { get; }
    public bool IncludeInWellKnownProviders { get; }
    public bool SupportsChildProviderIds { get; }
    public IReadOnlyCollection<string> HandledProviderIds { get; }
    public IReadOnlyDictionary<string, string> DisplayNameOverrides { get; }

    private readonly HashSet<string> _handledProviderIds;

    public ProviderDefinition(
        string providerId,
        string displayName,
        PlanType planType,
        bool isQuotaBased,
        string defaultConfigType,
        string? logoKey = null,
        bool autoIncludeWhenUnconfigured = false,
        bool includeInWellKnownProviders = false,
        IEnumerable<string>? handledProviderIds = null,
        IReadOnlyDictionary<string, string>? displayNameOverrides = null,
        bool supportsChildProviderIds = false)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id cannot be empty.", nameof(providerId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        ProviderId = providerId;
        DisplayName = displayName;
        PlanType = planType;
        IsQuotaBased = isQuotaBased;
        DefaultConfigType = defaultConfigType;
        LogoKey = logoKey;
        AutoIncludeWhenUnconfigured = autoIncludeWhenUnconfigured;
        IncludeInWellKnownProviders = includeInWellKnownProviders;
        SupportsChildProviderIds = supportsChildProviderIds;

        var normalizedHandledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            providerId
        };

        if (handledProviderIds != null)
        {
            foreach (var handledId in handledProviderIds)
            {
                if (!string.IsNullOrWhiteSpace(handledId))
                {
                    normalizedHandledIds.Add(handledId);
                }
            }
        }

        _handledProviderIds = normalizedHandledIds;
        HandledProviderIds = normalizedHandledIds.ToArray();
        DisplayNameOverrides = displayNameOverrides ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool HandlesProviderId(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return false;
        }

        if (_handledProviderIds.Contains(providerId))
        {
            return true;
        }

        if (!SupportsChildProviderIds)
        {
            return false;
        }

        return _handledProviderIds.Any(handled =>
            providerId.StartsWith($"{handled}.", StringComparison.OrdinalIgnoreCase));
    }

    public string? ResolveDisplayName(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        if (DisplayNameOverrides.TryGetValue(providerId, out var mapped))
        {
            return mapped;
        }

        if (_handledProviderIds.Contains(providerId))
        {
            return DisplayName;
        }

        return null;
    }
}
