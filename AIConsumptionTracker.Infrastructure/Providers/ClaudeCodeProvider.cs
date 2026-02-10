using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;

namespace AIConsumptionTracker.Infrastructure.Providers;

public class ClaudeCodeProvider : IProviderService
{
    public string ProviderId => "claude-code";
    private readonly ILogger<ClaudeCodeProvider> _logger;
    private readonly HttpClient _httpClient;

    public ClaudeCodeProvider(ILogger<ClaudeCodeProvider> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ProviderUsage>> GetUsageAsync(ProviderConfig config, Action<ProviderUsage>? progressCallback = null)
    {
        // Check if API key is configured
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            return new[] { new ProviderUsage
            {
                ProviderId = ProviderId,
                ProviderName = "Claude Code",
                IsAvailable = false,
                Description = "No API key configured",
                IsQuotaBased = false,
                PaymentType = PaymentType.UsageBased
            }};
        }

        // Try to get usage from Anthropic API first
        try
        {
            var apiUsage = await GetUsageFromApiAsync(config.ApiKey);
            if (apiUsage != null)
            {
                return new[] { apiUsage };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Claude usage from API, falling back to CLI");
        }

        // Fall back to CLI if API fails
        return await GetUsageFromCliAsync(config);
    }

    private async Task<ProviderUsage?> GetUsageFromApiAsync(string apiKey)
    {
        try
        {
            // Anthropic API for usage - using the admin/usage endpoint
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Anthropic API returned {response.StatusCode}: {errorContent}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var usageData = JsonSerializer.Deserialize<AnthropicUsageResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (usageData == null)
            {
                return null;
            }

            // Calculate totals from the usage data
            double totalCost = 0;
            double totalTokens = 0;
            
            if (usageData.Usage != null)
            {
                foreach (var item in usageData.Usage)
                {
                    totalCost += item.CostUsd;
                    totalTokens += item.InputTokens + item.OutputTokens;
                }
            }

            return new ProviderUsage
            {
                ProviderId = ProviderId,
                ProviderName = "Claude Code",
                UsagePercentage = 0, // No limit info from API
                CostUsed = totalCost,
                CostLimit = 0,
                UsageUnit = "USD",
                IsQuotaBased = false,
                PaymentType = PaymentType.UsageBased,
                IsAvailable = true,
                Description = $"${totalCost:F2} total cost | {totalTokens:N0} tokens"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Anthropic API");
            return null;
        }
    }

    private async Task<IEnumerable<ProviderUsage>> GetUsageFromCliAsync(ProviderConfig config)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "claude",
                    Arguments = "usage",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    // CLI not found, but key is configured - show as available
                    return new[] { new ProviderUsage
                    {
                        ProviderId = ProviderId,
                        ProviderName = "Claude Code",
                        IsAvailable = true,
                        Description = "Connected (API key configured)",
                        IsQuotaBased = false,
                        PaymentType = PaymentType.UsageBased
                    }};
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning($"Claude Code CLI failed: {error}");
                    // CLI failed, but key is configured - show as available
                    return new[] { new ProviderUsage
                    {
                        ProviderId = ProviderId,
                        ProviderName = "Claude Code",
                        IsAvailable = true,
                        Description = "Connected (API key configured)",
                        IsQuotaBased = false,
                        PaymentType = PaymentType.UsageBased
                    }};
                }

                return new[] { ParseCliOutput(output) };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run Claude Code CLI");
                // Exception occurred, but key is configured - show as available
                return new[] { new ProviderUsage
                {
                    ProviderId = ProviderId,
                    ProviderName = "Claude Code",
                    IsAvailable = true,
                    Description = "Connected (API key configured)",
                    IsQuotaBased = false,
                    PaymentType = PaymentType.UsageBased
                }};
            }
        });
    }

    private ProviderUsage ParseCliOutput(string output)
    {
        // Parse Claude Code usage output
        double currentUsage = 0;
        double budgetLimit = 0;

        var usageMatch = Regex.Match(output, @"Current Usage[:\s]+\$?([0-9.]+)", RegexOptions.IgnoreCase);
        if (usageMatch.Success)
        {
            double.TryParse(usageMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out currentUsage);
        }

        var budgetMatch = Regex.Match(output, @"Budget Limit[:\s]+\$?([0-9.]+)", RegexOptions.IgnoreCase);
        if (budgetMatch.Success)
        {
            double.TryParse(budgetMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out budgetLimit);
        }

        var remainingMatch = Regex.Match(output, @"Remaining[:\s]+\$?([0-9.]+)", RegexOptions.IgnoreCase);
        if (remainingMatch.Success && budgetLimit == 0)
        {
            double remaining;
            if (double.TryParse(remainingMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out remaining))
            {
                budgetLimit = currentUsage + remaining;
            }
        }

        double usagePercentage = budgetLimit > 0 ? (currentUsage / budgetLimit) * 100.0 : 0;

        return new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = "Claude Code",
            UsagePercentage = Math.Min(usagePercentage, 100),
            CostUsed = currentUsage,
            CostLimit = budgetLimit,
            UsageUnit = "USD",
            IsQuotaBased = false,
            PaymentType = PaymentType.UsageBased,
            IsAvailable = true,
            Description = budgetLimit > 0 
                ? $"${currentUsage:F2} used of ${budgetLimit:F2} limit"
                : $"${currentUsage:F2} used"
        };
    }

    private class AnthropicUsageResponse
    {
        public List<AnthropicUsageItem>? Usage { get; set; }
    }

    private class AnthropicUsageItem
    {
        public string? Model { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public double CostUsd { get; set; }
        public string? Timestamp { get; set; }
    }
}
