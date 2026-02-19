using Microsoft.Extensions.Logging;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;

namespace AIConsumptionTracker.Infrastructure.Providers;

public class GitHubCopilotProvider : IProviderService
{
    public string ProviderId => "github-copilot";
    private readonly IGitHubAuthService _authService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubCopilotProvider> _logger;

    public GitHubCopilotProvider(HttpClient httpClient, ILogger<GitHubCopilotProvider> logger, IGitHubAuthService authService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authService = authService;
    }

    public async Task<IEnumerable<ProviderUsage>> GetUsageAsync(ProviderConfig config, Action<ProviderUsage>? progressCallback = null)
    {
        var token = _authService.GetCurrentToken();
        
        if (string.IsNullOrEmpty(token))
        {
            // If explicit API key is provided in config (e.g. from environment or manual entry), use that.
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                token = config.ApiKey;
            }
        }

        bool isAvailable = !string.IsNullOrEmpty(token);
        if (!isAvailable)
        {
             return new[] { new ProviderUsage
             {
                 ProviderId = ProviderId,
                 ProviderName = "GitHub Copilot",
                 IsAvailable = false,
                 Description = "Not authenticated. Please login in Settings.",
                 IsQuotaBased = true,
                 PlanType = PlanType.Coding
             }};
        }

        string description = "Authenticated";
        string username = "User";
        string planName = "";
        DateTime? resetTime = GetNextCopilotMonthlyResetLocal();
        double percentage = 0;
        double costUsed = 0;
        double costLimit = 0;
        bool hasCopilotQuotaData = false;
        bool hasRateLimitData = false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("AIConsumptionTracker", "1.0"));

            var response = await _httpClient.SendAsync(request);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new[] { new ProviderUsage
                {
                    ProviderId = ProviderId,
                    ProviderName = "GitHub Copilot",
                    IsAvailable = false,
                    Description = "Authentication failed (401). Please re-login.",
                    IsQuotaBased = true,
                    PlanType = PlanType.Coding
                }};
            }

            if (response.IsSuccessStatusCode)
            {
                 var json = await response.Content.ReadAsStringAsync();
                 using (var doc = System.Text.Json.JsonDocument.Parse(json))
                 {
                     if (doc.RootElement.TryGetProperty("login", out var loginElement))
                     {
                         username = loginElement.GetString() ?? "User";
                     }
                     
                     // Try to get Copilot specific plan info via internal token
                     try 
                     {
                         var internalRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/copilot_internal/v2/token");
                         internalRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                         internalRequest.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("AIConsumptionTracker", "1.0"));
                         
                         var internalResponse = await _httpClient.SendAsync(internalRequest);
                         if (internalResponse.IsSuccessStatusCode)
                         {
                             var internalJson = await internalResponse.Content.ReadAsStringAsync();
                             using (var internalDoc = System.Text.Json.JsonDocument.Parse(internalJson))
                             {
                                 string sku = "";
                                 if (internalDoc.RootElement.TryGetProperty("sku", out var skuProp))
                                     sku = skuProp.GetString() ?? "";
                                     
                                 // "copilot_individual", "copilot_business", etc.
                                  planName = NormalizeCopilotPlanName(sku);
                              }
                              description = $"Authenticated as {username} ({planName})";
                          }
                         else 
                         {
                             description = $"Authenticated as {username}";
                         }
                     }
                      catch 
                      {
                          description = $"Authenticated as {username}";
                      }

                      // Prefer Copilot quota snapshot data over generic GitHub core rate limits.
                      try
                      {
                          var quotaRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/copilot_internal/user");
                          quotaRequest.Headers.TryAddWithoutValidation("Authorization", $"token {token}");
                          quotaRequest.Headers.TryAddWithoutValidation("Accept", "application/json");
                          quotaRequest.Headers.TryAddWithoutValidation("Editor-Version", "vscode/1.96.2");
                          quotaRequest.Headers.TryAddWithoutValidation("X-Github-Api-Version", "2025-04-01");
                          quotaRequest.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("AIConsumptionTracker", "1.0"));

                          var quotaResponse = await _httpClient.SendAsync(quotaRequest);
                          if (quotaResponse.IsSuccessStatusCode)
                          {
                              var quotaJson = await quotaResponse.Content.ReadAsStringAsync();
                              using var quotaDoc = System.Text.Json.JsonDocument.Parse(quotaJson);
                              var root = quotaDoc.RootElement;

                              if (root.TryGetProperty("copilot_plan", out var planProp))
                              {
                                  var quotaPlan = NormalizeCopilotPlanName(planProp.GetString() ?? "");
                                  if (!string.IsNullOrWhiteSpace(quotaPlan))
                                  {
                                      planName = quotaPlan;
                                  }
                              }

                              if (root.TryGetProperty("quota_reset_date", out var resetProp))
                              {
                                  var resetText = resetProp.GetString();
                                  if (!string.IsNullOrWhiteSpace(resetText) &&
                                      DateTime.TryParse(
                                          resetText,
                                          System.Globalization.CultureInfo.InvariantCulture,
                                          System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                                          out var parsedResetUtc))
                                  {
                                      resetTime = parsedResetUtc.ToLocalTime();
                                  }
                              }

                              if (root.TryGetProperty("quota_snapshots", out var snapshots) &&
                                  snapshots.TryGetProperty("premium_interactions", out var premium) &&
                                  premium.TryGetProperty("entitlement", out var entitlementProp) &&
                                  premium.TryGetProperty("remaining", out var remainingProp) &&
                                  entitlementProp.TryGetDouble(out var entitlement) &&
                                  remainingProp.TryGetDouble(out var remaining) &&
                                  entitlement > 0)
                              {
                                  var normalizedRemaining = Math.Clamp(remaining, 0, entitlement);
                                  var used = Math.Max(0, entitlement - normalizedRemaining);
                                  costLimit = entitlement;
                                  costUsed = used;
                                  percentage = UsageMath.CalculateRemainingPercent(used, entitlement);
                                  hasCopilotQuotaData = true;
                              }
                          }
                      }
                      catch
                      {
                          // Continue with fallback sources.
                      }
                  }
            }
            
            // Fallback: fetch generic GitHub core rate limits to show usage.
            if (!hasCopilotQuotaData)
            {
                try 
                {
                    var rateRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/rate_limit");
                    rateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    rateRequest.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("AIConsumptionTracker", "1.0"));
                    
                    var rateResponse = await _httpClient.SendAsync(rateRequest);
                    if (rateResponse.IsSuccessStatusCode)
                    {
                        var rateJson = await rateResponse.Content.ReadAsStringAsync();
                        using (var rateDoc = System.Text.Json.JsonDocument.Parse(rateJson))
                        {
                            if (rateDoc.RootElement.TryGetProperty("resources", out var res) && 
                                res.TryGetProperty("core", out var core))
                            {
                                int limit = core.GetProperty("limit").GetInt32();
                                int remaining = core.GetProperty("remaining").GetInt32();
                                int used = Math.Max(0, limit - remaining);
                                
                                hasRateLimitData = true;
                                
                                // Show REMAINING percentage (like quota providers)
                                costLimit = limit;
                                costUsed = used;
                                percentage = UsageMath.CalculateRemainingPercent(used, limit);  // REMAINING %
                            }
                        }
                    }
                }
                catch {} // Ignore fallback rate limit fetch errors
            }

            if (!response.IsSuccessStatusCode)
            {
                 description = $"Error: {response.StatusCode}";
                 isAvailable = false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error fetching GitHub profile");
            description = "Network Error: Unable to reach GitHub";
            isAvailable = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch GitHub profile");
            description = $"Error: {ex.Message}";
            isAvailable = false;
        }

        string finalDescription;
        if (hasCopilotQuotaData)
        {
             finalDescription = $"Premium Requests: {costLimit - costUsed:F0}/{costLimit:F0} Remaining";
             if (!string.IsNullOrEmpty(planName))
             {
                 finalDescription += $" ({planName})";
             }
        }
        else if (hasRateLimitData)
        {
             finalDescription = $"API Rate Limit: {costLimit - costUsed:F0}/{costLimit:F0} Remaining";
             if (!string.IsNullOrEmpty(planName))
             {
                 finalDescription += $" ({planName})";
             }
        }
        else
        {
             finalDescription = description;
        }

        return new[] { new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = "GitHub Copilot",
            AccountName = username, 
            IsAvailable = isAvailable,
            Description = finalDescription,
            RequestsPercentage = percentage,  // REMAINING % for quota-like behavior
            RequestsAvailable = costLimit,
            RequestsUsed = costUsed,
            UsageUnit = "Requests",
            PlanType = PlanType.Coding, 
            IsQuotaBased = true,
            AuthSource = string.IsNullOrEmpty(planName) ? "Unknown" : planName,
            NextResetTime = resetTime
        }};
    }

    private static DateTime GetNextCopilotMonthlyResetLocal()
    {
        var utcNow = DateTime.UtcNow;
        var nextResetUtc = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return nextResetUtc.ToLocalTime();
    }

    private static string NormalizeCopilotPlanName(string plan)
    {
        return plan switch
        {
            "copilot_individual" => "Copilot Individual",
            "copilot_business" => "Copilot Business",
            "copilot_enterprise" => "Copilot Enterprise",
            "copilot_free" => "Copilot Free",
            _ => plan
        };
    }
}
