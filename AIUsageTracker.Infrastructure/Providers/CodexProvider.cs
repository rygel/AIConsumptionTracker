using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.Infrastructure.Providers;

public class CodexProvider : IProviderService
{
    private const string UsageEndpoint = "https://chatgpt.com/backend-api/wham/usage";
    private const string ProfileClaimKey = "https://api.openai.com/profile";
    private const string AuthClaimKey = "https://api.openai.com/auth";
    public static ProviderDefinition StaticDefinition { get; } = new(
        providerId: "codex",
        displayName: "OpenAI Codex",
        planType: PlanType.Coding,
        isQuotaBased: true,
        defaultConfigType: "quota-based",
        handledProviderIds: new[] { "openai" },
        includeInWellKnownProviders: true,
        supportsChildProviderIds: true);

    public ProviderDefinition Definition => StaticDefinition;
    public string ProviderId => StaticDefinition.ProviderId;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CodexProvider> _logger;
    private readonly string _authFilePath;

    public CodexProvider(HttpClient httpClient, ILogger<CodexProvider> logger)
        : this(httpClient, logger, null)
    {
    }

    public CodexProvider(HttpClient httpClient, ILogger<CodexProvider> logger, string? authFilePath)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authFilePath = string.IsNullOrWhiteSpace(authFilePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json")
            : authFilePath;
    }

    public async Task<IEnumerable<ProviderUsage>> GetUsageAsync(ProviderConfig config, Action<ProviderUsage>? progressCallback = null)
    {
        try
        {
            var auth = await LoadNativeAuthAsync();
            var accessToken = auth?.AccessToken;
            var accountId = auth?.AccountId;
            var authIdentity = auth?.Identity;

            // Allow explicit config/env token as fallback when auth.json is not available.
            if (string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(config.ApiKey))
            {
                accessToken = config.ApiKey;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new[] { CreateUnavailableUsage("Codex auth token not found (~/.codex/auth.json or session token)") };
            }

            var resolvedAccessToken = accessToken!;
            var (email, jwtPlanType) = DecodeJwtClaims(resolvedAccessToken);

            using var request = CreateUsageRequest(resolvedAccessToken, accountId);
            using var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var unavailableFromStatus = CreateUnavailableUsageFromStatus(response);
            if (unavailableFromStatus != null)
            {
                return new[] { unavailableFromStatus };
            }

            using var jsonDoc = JsonDocument.Parse(content);
            if (TryGetErrorDetailMessage(jsonDoc.RootElement, out var detailMessage))
            {
                return new[] { CreateUnavailableUsage(detailMessage) };
            }

            return BuildUsages(jsonDoc.RootElement, email, jwtPlanType, authIdentity, accountId, config);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Codex native usage response");
            return new[] { CreateUnavailableUsage("Invalid Codex usage response format") };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Codex native usage lookup failed");
            return new[] { CreateUnavailableUsage($"Codex native lookup failed: {ex.Message}") };
        }
    }

    private static HttpRequestMessage CreateUsageRequest(string accessToken, string? accountId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", accessToken)
            }
        };

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
        }

        return request;
    }

    private ProviderUsage? CreateUnavailableUsageFromStatus(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return CreateUnavailableUsage($"Authentication failed ({(int)response.StatusCode})");
        }

        if (!response.IsSuccessStatusCode)
        {
            return CreateUnavailableUsage($"Usage request failed ({(int)response.StatusCode})");
        }

        return null;
    }

    private static bool TryGetErrorDetailMessage(JsonElement root, out string message)
    {
        if (root.TryGetProperty("detail", out var detail) &&
            detail.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(detail.GetString()))
        {
            message = detail.GetString()!;
            return true;
        }

        message = string.Empty;
        return false;
    }

    private List<ProviderUsage> BuildUsages(
        JsonElement root,
        string? jwtEmail,
        string? jwtPlanType,
        string? authIdentity,
        string? accountId,
        ProviderConfig config)
    {
        var planType = ReadString(root, "plan_type") ?? jwtPlanType ?? "unknown";
        
        var primaryUsedPercent = ReadDouble(root, "rate_limit", "primary_window", "used_percent") ?? 0.0;
        var primaryResetSeconds = ReadDouble(root, "rate_limit", "primary_window", "reset_after_seconds");
        var secondaryUsedPercent = ReadDouble(root, "rate_limit", "secondary_window", "used_percent");
        var secondaryResetSeconds = ReadDouble(root, "rate_limit", "secondary_window", "reset_after_seconds");
        
        var additionalWindows = ExtractAdditionalWindows(root);
        var modelNames = ResolveModelNames(root, additionalWindows);
        var accountIdentity = ResolveAccountIdentity(root, jwtEmail, authIdentity, accountId);

        var remainingPercent = Math.Clamp(100.0 - primaryUsedPercent, 0.0, 100.0);
        var details = BuildDetails(
            primaryUsedPercent,
            primaryResetSeconds,
            secondaryUsedPercent,
            secondaryResetSeconds,
            additionalWindows,
            modelNames,
            root);
        
        var nextResetTime = ResolveNextResetTime(primaryResetSeconds, additionalWindows.FirstOrDefault().ResetAfterSeconds);
        var usages = new List<ProviderUsage>();

        // Resolve primary model identity from config if available
        var (primaryId, primaryName) = ResolveModelIdentity(modelNames.PrimaryModelName, $"{ProviderId}.primary", "Codex [OpenAI Codex]", config);

        // Add the primary model as a child bar
        usages.Add(new ProviderUsage
        {
            ProviderId = primaryId,
            ProviderName = primaryName,
            RequestsPercentage = remainingPercent,
            RequestsUsed = 100.0 - remainingPercent,
            RequestsAvailable = 100.0,
            UsageUnit = "Quota %",
            IsQuotaBased = true,
            PlanType = PlanType.Coding,
            IsAvailable = true,
            Description = $"{remainingPercent:F0}% remaining ({primaryUsedPercent:F0}% used) | Plan: {planType}",
            AccountName = accountIdentity ?? string.Empty,
            AuthSource = $"Codex Native ({planType})",
            NextResetTime = ResolveDetailResetTime(primaryResetSeconds),
            Details = details // Keep details on the primary child so Settings can find them
        });

        foreach (var window in additionalWindows)
        {
            if (window.UsedPercent.HasValue)
            {
                usages.Add(CreateAdditionalUsage(window, planType, accountIdentity, config));
            }
        }

        return usages;
    }

    private static ProviderUsage CreateAdditionalUsage(
        AdditionalWindow window,
        string planType,
        string? accountIdentity,
        ProviderConfig config)
    {
        var usedPercent = Math.Clamp(window.UsedPercent ?? 0.0, 0.0, 100.0);
        var remainingPercent = Math.Clamp(100.0 - usedPercent, 0.0, 100.0);

        var modelName = NormalizeModelName(window.ModelName, null) ?? window.Label ?? "OpenAI (Additional)";
        var suffix = " [OpenAI Codex]";
        var displayName = modelName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? modelName : modelName + suffix;
        var providerId = $"codex.{modelName.ToLowerInvariant().Replace(" ", "-")}";
        var (sparkId, sparkName) = ResolveModelIdentity(modelName, providerId, displayName, config);

        return new ProviderUsage
        {
            ProviderId = sparkId,
            ProviderName = sparkName,
            RequestsPercentage = remainingPercent,
            RequestsUsed = usedPercent,
            RequestsAvailable = 100.0,
            UsageUnit = "Quota %",
            IsQuotaBased = true,
            PlanType = PlanType.Coding,
            IsAvailable = true,
            Description = $"{remainingPercent:F0}% remaining ({usedPercent:F0}% used) | Plan: {planType}",
            AccountName = accountIdentity ?? string.Empty,
            AuthSource = $"Codex Native ({planType})",
            NextResetTime = ResolveDetailResetTime(window.ResetAfterSeconds)
        };
    }

    private static string? ResolveAccountIdentity(
        JsonElement root,
        string? jwtEmail,
        string? authIdentity,
        string? accountId)
    {
        // Try recursive scan first for robustness (similar to Settings UI)
        var discovered = FindIdentityInJson(root);
        if (IsEmailLike(discovered)) return discovered;

        foreach (var key in new[] { "email", "upn", "preferred_username" })
        {
            if (root.TryGetProperty(key, out var claimElement) && claimElement.ValueKind == JsonValueKind.String)
            {
                var value = claimElement.GetString();
                if (IsEmailLike(value))
                {
                    return value;
                }
            }
        }

        var profileEmail = ReadString(root, ProfileClaimKey, "email");
        if (IsEmailLike(profileEmail))
        {
            return profileEmail;
        }

        if (IsEmailLike(jwtEmail))
        {
            return jwtEmail;
        }

        if (IsEmailLike(authIdentity))
        {
            return authIdentity;
        }

        // If no email found, try discovered non-email identity
        if (!string.IsNullOrWhiteSpace(discovered)) return discovered;

        foreach (var key in new[] { "username", "login", "name" })
        {
            if (root.TryGetProperty(key, out var claimElement) && claimElement.ValueKind == JsonValueKind.String)
            {
                var value = claimElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(authIdentity))
        {
            return authIdentity;
        }

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            return accountId;
        }

        return null;
    }

    private static string? FindIdentityInJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = prop.Value.GetString();
                        if (string.IsNullOrWhiteSpace(value)) continue;

                        var key = prop.Name.ToLowerInvariant();
                        // Priority 1: Direct email match
                        if (key.Contains("email") && IsEmailLike(value)) return value;
                        // Priority 2: Standard identity keys
                        if (key == "upn" || key == "preferred_username" || key == "login" || key == "username") return value;
                    }

                    var nested = FindIdentityInJson(prop.Value);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindIdentityInJson(item);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
                break;
            
            case JsonValueKind.String:
                var s = element.GetString();
                if (IsEmailLike(s)) return s;
                break;
        }
        return null;
    }

    private static string BuildUsageDescription(
        double remainingPercent,
        double primaryUsedPercent,
        List<AdditionalWindow> additionalWindows,
        string planType)
    {
        var description = $"{remainingPercent:F0}% remaining ({primaryUsedPercent:F0}% used) | Plan: {planType}";
        foreach (var window in additionalWindows)
        {
            if (window.UsedPercent.HasValue)
            {
                description += $" | {window.Label ?? "Additional"}: {window.UsedPercent.Value:F0}% used";
            }
        }

        return description;
    }

    private async Task<CodexAuth?> LoadNativeAuthAsync()
    {
        foreach (var path in GetAuthFileCandidates())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Priority: Use the same robust recursive scan for auth file identity
                var discoveredIdentity = FindIdentityInJson(root);

                if (root.TryGetProperty("tokens", out var tokensElement) &&
                    tokensElement.ValueKind == JsonValueKind.Object)
                {
                    var accessToken = ReadString(tokensElement, "access_token");
                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        var idToken = ReadString(tokensElement, "id_token");
                        var accountId = ReadString(tokensElement, "account_id");
                        var identity = discoveredIdentity ?? ResolveIdentityFromAuthPayload(root, accessToken, idToken);
                        return new CodexAuth
                        {
                            AccessToken = accessToken,
                            AccountId = accountId,
                            Identity = identity
                        };
                    }
                }

                if (root.TryGetProperty("openai", out var openAiElement) &&
                    openAiElement.ValueKind == JsonValueKind.Object)
                {
                    var accessToken = ReadString(openAiElement, "access");
                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        var idToken = ReadString(openAiElement, "id_token");
                        var accountId = ReadString(openAiElement, "accountId") ?? ReadString(openAiElement, "account_id");
                        var identity = discoveredIdentity ?? ResolveIdentityFromAuthPayload(openAiElement, accessToken, idToken);
                        return new CodexAuth
                        {
                            AccessToken = accessToken,
                            AccountId = accountId,
                            Identity = identity
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read Codex auth file at {Path}", path);
            }
        }

        return null;
    }

    private IEnumerable<string> GetAuthFileCandidates()
    {
        if (!string.IsNullOrWhiteSpace(_authFilePath))
        {
            yield return _authFilePath;
            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".codex", "auth.json");
        yield return Path.Combine(home, ".local", "share", "opencode", "auth.json");
        yield return Path.Combine(home, ".opencode", "auth.json");

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                yield return Path.Combine(appData, "codex", "auth.json");
                yield return Path.Combine(appData, "opencode", "auth.json");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                yield return Path.Combine(localAppData, "opencode", "auth.json");
            }
        }
    }

    private static string? ResolveIdentityFromAuthPayload(JsonElement source, string accessToken, string? idToken = null)
    {
        foreach (var claim in new[] { "email", "upn", "preferred_username", "username", "login", "name" })
        {
            var value = ReadString(source, claim);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsEmailLike(value))
            {
                return value;
            }
        }

        var nestedProfile = ReadString(source, "profile", "email");
        if (IsEmailLike(nestedProfile))
        {
            return nestedProfile;
        }

        if (!string.IsNullOrWhiteSpace(idToken))
        {
            var (emailFromIdToken, _) = DecodeJwtClaims(idToken);
            if (!string.IsNullOrWhiteSpace(emailFromIdToken))
            {
                return emailFromIdToken;
            }
        }

        var (emailFromJwt, _) = DecodeJwtClaims(accessToken);
        if (!string.IsNullOrWhiteSpace(emailFromJwt))
        {
            return emailFromJwt;
        }

        return null;
    }

    private static List<ProviderUsageDetail> BuildDetails(
        double primaryUsedPercent,
        double? primaryResetSeconds,
        double? secondaryUsedPercent,
        double? secondaryResetSeconds,
        List<AdditionalWindow> additionalWindows,
        (string PrimaryModelName, string? SparkModelName) modelNames,
        JsonElement root)
    {
        var primaryRemaining = Math.Clamp(100.0 - primaryUsedPercent, 0.0, 100.0);
        var details = new List<ProviderUsageDetail>();

        // Add the primary model as a "Model" detail so it shows up in Settings and can have a sub-tray
        details.Add(new ProviderUsageDetail
        {
            Name = "Codex", // Simplified for sub-tray
            Used = $"{primaryRemaining:F0}%",
            DetailType = ProviderUsageDetailType.Model,
            WindowKind = WindowKind.Primary
        });

        details.Add(new ProviderUsageDetail
        {
            Name = "5-hour quota",
            Used = $"{primaryRemaining:F0}% remaining ({primaryUsedPercent:F0}% used)",
            Description = FormatResetDescription(primaryResetSeconds),
            NextResetTime = ResolveDetailResetTime(primaryResetSeconds),
            DetailType = ProviderUsageDetailType.QuotaWindow,
            WindowKind = WindowKind.Primary
        });

        if (secondaryUsedPercent.HasValue)
        {
            var secondaryRemaining = Math.Clamp(100.0 - secondaryUsedPercent.Value, 0.0, 100.0);
            details.Add(new ProviderUsageDetail
            {
                Name = "Weekly quota",
                Used = $"{secondaryRemaining:F0}% remaining ({secondaryUsedPercent.Value:F0}% used)",
                Description = FormatResetDescription(secondaryResetSeconds),
                NextResetTime = ResolveDetailResetTime(secondaryResetSeconds),
                DetailType = ProviderUsageDetailType.QuotaWindow,
                WindowKind = WindowKind.Secondary
            });
        }

        foreach (var window in additionalWindows)
        {
            if (window.UsedPercent.HasValue)
            {
                var remaining = Math.Clamp(100.0 - window.UsedPercent.Value, 0.0, 100.0);
                var modelName = NormalizeModelName(window.ModelName, null) ?? window.Label ?? "Additional quota";
                details.Add(new ProviderUsageDetail
                {
                    Name = modelName,
                    Used = $"{remaining:F0}% remaining ({window.UsedPercent.Value:F0}% used)",
                    Description = FormatResetDescription(window.ResetAfterSeconds),
                    NextResetTime = ResolveDetailResetTime(window.ResetAfterSeconds),
                    DetailType = ProviderUsageDetailType.Model,
                    WindowKind = window.Label?.Contains("spark", StringComparison.OrdinalIgnoreCase) == true ? WindowKind.Spark : WindowKind.None
                });
            }
        }

        var creditsBalance = ReadDouble(root, "credits", "balance");
        var creditsUnlimited = ReadBool(root, "credits", "unlimited");
        if (creditsBalance.HasValue || creditsUnlimited.HasValue)
        {
            var creditValue = creditsUnlimited == true
                ? "Unlimited"
                : creditsBalance?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown";

            details.Add(new ProviderUsageDetail
            {
                Name = "Credits",
                Used = creditValue,
                DetailType = ProviderUsageDetailType.Credit,
                WindowKind = WindowKind.None
            });
        }

        return details;
    }

    private static (string PrimaryModelName, string? SparkModelName) ResolveModelNames(JsonElement root, List<AdditionalWindow> additionalWindows)
    {
        var primaryRaw = ReadString(root, "model_name")
                         ?? ReadString(root, "model")
                         ?? ReadString(root, "rate_limit", "primary_window", "model_name")
                         ?? ReadString(root, "rate_limit", "primary_window", "model")
                         ?? ReadString(root, "rate_limit", "primary_window", "limit_name");
        var primaryModelName = NormalizeModelName(primaryRaw, "OpenAI (Codex)") ?? "OpenAI (Codex)";

        string? sparkModelName = null;
        var sparkWindow = additionalWindows.FirstOrDefault(w => w.Label?.Contains("spark", StringComparison.OrdinalIgnoreCase) == true);
        if (sparkWindow.UsedPercent.HasValue)
        {
            sparkModelName = ResolveSparkModelName(sparkWindow);
        }

        return (primaryModelName, sparkModelName);
    }

    private static string? ResolveSparkModelName(AdditionalWindow window)
    {
        var explicitModelName = NormalizeModelName(window.ModelName, null);
        if (!string.IsNullOrWhiteSpace(explicitModelName))
        {
            return explicitModelName;
        }

        var label = NormalizeModelName(window.Label, null);
        if (!string.IsNullOrWhiteSpace(label) && LooksLikeModelName(label))
        {
            return label;
        }

        return null;
    }

    private static string? NormalizeModelName(string? raw, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var normalized = raw.Trim();
        normalized = normalized.Replace('_', '-');
        normalized = normalized.Replace("  ", " ");
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static bool LooksLikeModelName(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var lower = label.ToLowerInvariant();
        return lower.Contains("gpt") ||
               lower.Contains("codex") ||
               lower.Contains("spark") ||
               lower.Contains("claude") ||
               lower.Contains("gemini") ||
               lower.Contains("sonnet");
    }

    private static (string? Email, string? PlanType) DecodeJwtClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return (null, null);
        }

        try
        {
            var payloadBytes = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            string? email = null;
            string? planType = null;

            foreach (var claim in new[] { "email", "upn", "preferred_username" })
            {
                if (root.TryGetProperty(claim, out var claimElement) && claimElement.ValueKind == JsonValueKind.String)
                {
                    var value = claimElement.GetString();
                    if (IsEmailLike(value))
                    {
                        email = value;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(email) &&
                root.TryGetProperty(ProfileClaimKey, out var profile) &&
                profile.ValueKind == JsonValueKind.Object)
            {
                foreach (var claim in new[] { "email", "username", "name" })
                {
                    var value = ReadString(profile, claim);
                    if (IsEmailLike(value))
                    {
                        email = value;
                        break;
                    }
                }
            }

            if (root.TryGetProperty(AuthClaimKey, out var auth) && auth.ValueKind == JsonValueKind.Object)
            {
                if (auth.TryGetProperty("chatgpt_plan_type", out var planTypeElement) && planTypeElement.ValueKind == JsonValueKind.String)
                {
                    planType = planTypeElement.GetString();
                }
            }

            return (email, planType);
        }
        catch
        {
            return (null, null);
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(normalized);
    }

    private static DateTime? ResolveNextResetTime(double? primaryResetSeconds, double? secondaryResetSeconds)
    {
        var resetSeconds = primaryResetSeconds ?? secondaryResetSeconds;
        if (!resetSeconds.HasValue || resetSeconds.Value <= 0)
        {
            return null;
        }

        return DateTime.UtcNow.AddSeconds(resetSeconds.Value).ToLocalTime();
    }

    private static string FormatResetDescription(double? resetAfterSeconds)
    {
        if (!resetAfterSeconds.HasValue || resetAfterSeconds.Value <= 0)
        {
            return string.Empty;
        }

        return $"Resets in {(int)resetAfterSeconds.Value}s";
    }

    private static DateTime? ResolveDetailResetTime(double? resetAfterSeconds)
    {
        if (!resetAfterSeconds.HasValue || resetAfterSeconds.Value <= 0)
        {
            return null;
        }

        return DateTime.UtcNow.AddSeconds(resetAfterSeconds.Value).ToLocalTime();
    }

    private static List<AdditionalWindow> ExtractAdditionalWindows(JsonElement root)
    {
        var results = new List<AdditionalWindow>();

        if (root.TryGetProperty("additional_rate_limits", out var additionalRateLimits) &&
            additionalRateLimits.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in additionalRateLimits.EnumerateArray())
            {
                var limitName = ReadString(item, "limit_name");
                if (string.IsNullOrWhiteSpace(limitName)) continue;

                if (item.TryGetProperty("rate_limit", out var rateLimit))
                {
                    var usedPercent = ReadDouble(rateLimit, "primary_window", "used_percent");
                    var resetAfterSeconds = ReadDouble(rateLimit, "primary_window", "reset_after_seconds");
                    if (usedPercent.HasValue || resetAfterSeconds.HasValue)
                    {
                        var modelName = ReadString(item, "model_name") ?? ReadString(item, "model");
                        results.Add(new AdditionalWindow(limitName, modelName, usedPercent, resetAfterSeconds));
                    }
                }
            }
        }

        if (root.TryGetProperty("rate_limit", out var rateLimitObj) && rateLimitObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in rateLimitObj.EnumerateObject())
            {
                // Skip primary window as it's handled separately
                if (property.Name.Equals("primary_window", StringComparison.OrdinalIgnoreCase)) continue;

                var usedPercent = ReadDouble(property.Value, "used_percent");
                var resetAfterSeconds = ReadDouble(property.Value, "reset_after_seconds");
                if (usedPercent.HasValue || resetAfterSeconds.HasValue)
                {
                    var modelName = ReadString(property.Value, "model_name") ?? ReadString(property.Value, "model");
                    results.Add(new AdditionalWindow(property.Name, modelName, usedPercent, resetAfterSeconds));
                }
            }
        }

        return results;
    }

    private static string? ReadString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static double? ReadDouble(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind == JsonValueKind.Number && current.TryGetDouble(out var number))
        {
            return number;
        }

        if (current.ValueKind == JsonValueKind.String &&
            double.TryParse(current.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool IsEmailLike(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains('@');
    }

    private ProviderUsage CreateUnavailableUsage(string message)
    {
        return new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = "OpenAI (Codex)",
            IsAvailable = false,
            IsQuotaBased = true,
            PlanType = PlanType.Coding,
            RequestsPercentage = 0,
            RequestsUsed = 0,
            RequestsAvailable = 100,
            UsageUnit = "Quota %",
            Description = message,
            AuthSource = "Codex Native"
        };
    }

    private sealed class CodexAuth
    {
        public string? AccessToken { get; set; }
        public string? AccountId { get; set; }
        public string? Identity { get; set; }
    }

    private readonly record struct AdditionalWindow(
        string? Label,
        string? ModelName,
        double? UsedPercent,
        double? ResetAfterSeconds);

    private static (string Id, string Name) ResolveModelIdentity(string internalModelName, string defaultId, string defaultName, ProviderConfig config)
    {
        if (config.Models == null || !config.Models.Any())
        {
            return (defaultId, defaultName);
        }

        var match = config.Models.FirstOrDefault(m =>
            m.Id.Equals(internalModelName, StringComparison.OrdinalIgnoreCase) ||
            m.Name.Equals(internalModelName, StringComparison.OrdinalIgnoreCase) ||
            m.Matches.Any(x => x.Equals(internalModelName, StringComparison.OrdinalIgnoreCase)));

        if (match != null)
        {
            return (match.Id, match.Name);
        }

        return (defaultId, defaultName);
    }
}


