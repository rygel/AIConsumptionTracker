using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;

namespace AIConsumptionTracker.Infrastructure.Providers;

public class OpenRouterProvider : IProviderService
{
    public string ProviderId => "openrouter";
    private readonly HttpClient _httpClient;

    public OpenRouterProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProviderUsage> GetUsageAsync(ProviderConfig config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            // If API key is missing, we can't fetch.
            // However, this might be a placeholder config?
            throw new ArgumentException("API Key not found for OpenRouter provider.");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/credits");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
             // Try fetching "key" endpoint if "credits" fails? 
             // Ref code fetches both credits and key info. Simplest is start with credits.
            throw new Exception($"OpenRouter API returned {response.StatusCode}");
        }

        var data = await response.Content.ReadFromJsonAsync<OpenRouterCreditsResponse>();
        
        if (data?.Data == null)
        {
            throw new Exception("Failed to parse OpenRouter credits response.");
        }

        var total = data.Data.TotalCredits;
        var used = data.Data.TotalUsage;
        var utilization = total > 0 ? (used / total) * 100.0 : 0;

        return new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = "OpenRouter",
            UsagePercentage = Math.Min(utilization, 100),
            CostUsed = used,
            CostLimit = total,
            UsageUnit = "Credits",
            IsQuotaBased = false,
            Description = $"{used:F2} / {total:F2} credits"
        };
    }

    private class OpenRouterCreditsResponse
    {
        [JsonPropertyName("data")]
        public CreditsData? Data { get; set; }
    }

    private class CreditsData
    {
        [JsonPropertyName("total_credits")]
        public double TotalCredits { get; set; }

        [JsonPropertyName("total_usage")]
        public double TotalUsage { get; set; }
    }
}

