using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;

namespace AIConsumptionTracker.Infrastructure.Providers;

public class GeminiProvider : IProviderService
{
    public string ProviderId => "gemini-cli";
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(HttpClient httpClient, ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProviderUsage> GetUsageAsync(ProviderConfig config)
    {
        // 1. Load Accounts
        var accounts = LoadAntigravityAccounts();
        if (accounts == null || accounts.Accounts == null || !accounts.Accounts.Any())
        {
             return new ProviderUsage
             {
                 ProviderId = ProviderId,
                 ProviderName = "Gemini CLI",
                 IsAvailable = false,
                 Description = "No Gemini accounts found"
             };
        }

        double minPercentage = 100.0;
        int successCount = 0;

        foreach (var account in accounts.Accounts)
        {
            try
            {
                // 2. Refresh Token
                var accessToken = await RefreshToken(account.RefreshToken);
                
                // 3. Fetch Quota
                var percentage = await FetchQuota(accessToken, account.ProjectId);
                minPercentage = Math.Min(minPercentage, percentage);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to fetch Gemini quota for {account.Email}");
            }
        }

        if (successCount == 0)
        {
             throw new Exception("Failed to fetch quota for any Gemini account");
        }

        return new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = "Gemini CLI",
            UsagePercentage = minPercentage,
            CostUsed = 100 - minPercentage,
            CostLimit = 100,
            UsageUnit = "Quota %",
            IsQuotaBased = true,
            AccountName = string.Join(", ", accounts.Accounts.Select(a => a.Email)),
            Description = $"{minPercentage:F1}% remaining (min)"
        };
    }

    private AntigravityAccounts? LoadAntigravityAccounts()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode", "antigravity-accounts.json");
        if (!File.Exists(path)) return null;

        try 
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AntigravityAccounts>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load antigravity-accounts.json");
            return null;
        }
    }

    private async Task<string> RefreshToken(string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", "1071006060591-tmhssin2h21lcre235vtolojh4g403ep.apps.googleusercontent.com" }, // Public CLI ID
            { "client_secret", "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" },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        });
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<GeminiTokenResponse>();
        return tokenResponse?.AccessToken ?? throw new Exception("Failed to retrieve access token");
    }

    private async Task<double> FetchQuota(string accessToken, string projectId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        // Body: {"project": "..."}
        request.Content = JsonContent.Create(new { project = projectId });

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<GeminiQuotaResponse>();
        if (data?.Buckets == null || !data.Buckets.Any())
        {
            // If no buckets, assume 100% or error? Reference code throws.
            // But maybe user has no quotas yet?
            return 100.0;
        }

        double minFrac = 1.0;
        foreach (var bucket in data.Buckets)
        {
            minFrac = Math.Min(minFrac, bucket.RemainingFraction);
        }
        return minFrac * 100.0;
    }

    // Models
    private class AntigravityAccounts
    {
        public List<Account>? Accounts { get; set; }
    }

    private class Account
    {
        public string Email { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string ProjectId { get; set; } = "";
    }

    private class GeminiTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private class GeminiQuotaResponse
    {
        [JsonPropertyName("buckets")]
        public List<Bucket>? Buckets { get; set; }
    }

    private class Bucket
    {
        [JsonPropertyName("remainingFraction")]
        public double RemainingFraction { get; set; }
    }
}

