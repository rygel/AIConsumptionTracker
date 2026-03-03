using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Infrastructure.Providers;

public abstract class ProviderBase : IProviderService
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;

    protected ProviderBase(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    public abstract string ProviderId { get; }

    public abstract ProviderDefinition Definition { get; }

    public abstract Task<IEnumerable<ProviderUsage>> GetUsageAsync(
        ProviderConfig config,
        Action<ProviderUsage>? progressCallback = null);

    protected ProviderUsage CreateUnavailableUsage(
        string description,
        int httpStatus = 503)
    {
        return new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = Definition.DisplayName,
            IsAvailable = false,
            Description = description,
            HttpStatus = httpStatus,
            PlanType = Definition.PlanType,
            IsQuotaBased = Definition.IsQuotaBased
        };
    }

    protected HttpRequestMessage CreateAuthorizedRequest(
        string url,
        string apiKey,
        HttpMethod? method = null,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, url)
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return request;
    }

    protected DateTime? ParseResetTime(string? resetTime)
    {
        if (string.IsNullOrEmpty(resetTime))
        {
            return null;
        }

        if (DateTime.TryParse(resetTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            return dt.ToLocalTime();
        }

        if (long.TryParse(resetTime, out var timestamp))
        {
            if (timestamp > 10000000000)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
            }
            else
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
            }
        }

        Logger.LogWarning("Failed to parse reset time: {ResetTime}", resetTime);
        return null;
    }

    protected string FormatResetTime(DateTime? resetTime)
    {
        if (!resetTime.HasValue)
        {
            return "";
        }

        var dt = resetTime.Value;
        return $" (Resets: {dt:MMM dd, yyyy HH:mm} Local)";
    }
}
