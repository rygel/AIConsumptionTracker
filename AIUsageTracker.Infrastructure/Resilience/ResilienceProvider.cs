// <copyright file="ResilienceProvider.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Infrastructure.Resilience
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using AIUsageTracker.Core.Interfaces;
    using AIUsageTracker.Infrastructure.Http;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Polly.CircuitBreaker;
    using Polly.Retry;

    public class ResilienceProvider : IResilienceProvider
    {
        private readonly ILogger<ResilienceProvider> _logger;
        private readonly ConcurrentDictionary<string, object> _policies = new();
        private readonly ResilientHttpClientOptions _defaultOptions;

        public ResilienceProvider(ILogger<ResilienceProvider> logger, ResilientHttpClientOptions? defaultOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultOptions = defaultOptions ?? new ResilientHttpClientOptions();
        }

        public IAsyncPolicy<T> GetPolicy<T>(string policyName)
        {
            return (IAsyncPolicy<T>)_policies.GetOrAdd(policyName, name => CreateDefaultPolicy<T>(name));
        }

        public IAsyncPolicy<T> GetProviderPolicy<T>(string providerId)
        {
            var policyName = $"provider_{providerId}";
            return (IAsyncPolicy<T>)_policies.GetOrAdd(policyName, name => CreateProviderSpecificPolicy<T>(providerId));
        }

        private IAsyncPolicy<T> CreateDefaultPolicy<T>(string name)
        {
            _logger.LogDebug("Creating new resilience policy: {PolicyName}", name);

            var retryPolicy = Policy<T>
                .Handle<HttpRequestException>()
                .OrResult(r => IsRetryable(r))
                .WaitAndRetryAsync(
                    _defaultOptions.MaxRetryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(_defaultOptions.BackoffBase, retryAttempt)),
                    onRetry: (outcome, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Policy {PolicyName} triggered retry {RetryCount}/{MaxRetries} after {Delay}s due to {Reason}",
                            name,
                            retryCount,
                            _defaultOptions.MaxRetryCount,
                            timeSpan.TotalSeconds,
                            GetReason(outcome));
                    });

            var circuitBreakerPolicy = Policy<T>
                .Handle<HttpRequestException>()
                .OrResult(r => IsCircuitBreakerTrigger(r))
                .CircuitBreakerAsync(
                    _defaultOptions.CircuitBreakerFailureThreshold,
                    _defaultOptions.CircuitBreakerDuration,
                    onBreak: (outcome, duration) =>
                    {
                        _logger.LogError(
                            "Circuit breaker in policy {PolicyName} opened for {Duration} due to {Reason}",
                            name,
                            duration,
                            GetReason(outcome));
                    },
                    onReset: () => _logger.LogInformation("Circuit breaker in policy {PolicyName} closed.", name),
                    onHalfOpen: () => _logger.LogDebug("Circuit breaker in policy {PolicyName} half-open.", name));

            return circuitBreakerPolicy.WrapAsync(retryPolicy);
        }

        private IAsyncPolicy<T> CreateProviderSpecificPolicy<T>(string providerId)
        {
            // Future enhancement: load provider-specific options from configuration
            return CreateDefaultPolicy<T>($"provider_{providerId}");
        }

        private bool IsRetryable(object? result)
        {
            if (result is HttpResponseMessage response)
            {
                return _defaultOptions.RetryStatusCodes.Contains(response.StatusCode);
            }
            return false;
        }

        private bool IsCircuitBreakerTrigger(object? result)
        {
            if (result is HttpResponseMessage response)
            {
                return _defaultOptions.CircuitBreakerStatusCodes.Contains(response.StatusCode);
            }
            return false;
        }

        private string GetReason<T>(DelegateResult<T> outcome)
        {
            if (outcome.Exception != null)
            {
                return outcome.Exception.Message;
            }

            if (outcome.Result is HttpResponseMessage response) // architecture-allow-sync-wait: Polly DelegateResult.Result is not sync-over-async.
            {
                return $"HTTP {response.StatusCode}";
            }

            return "Unknown error";
        }
    }
}
