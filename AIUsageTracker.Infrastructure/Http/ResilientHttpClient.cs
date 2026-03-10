// <copyright file="ResilientHttpClient.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Infrastructure.Http
{
    using System.Net;
    using AIUsageTracker.Core.Interfaces;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Polly.CircuitBreaker;
    using Polly.Retry;

    public class ResilientHttpClient : IResilientHttpClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IResilienceProvider _resilienceProvider;
        private readonly ILogger<ResilientHttpClient> _logger;
        private bool _disposed;

        public ResilientHttpClient(
            HttpClient httpClient,
            IResilienceProvider resilienceProvider,
            ILogger<ResilientHttpClient> logger)
        {
            this._httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this._resilienceProvider = resilienceProvider ?? throw new ArgumentNullException(nameof(resilienceProvider));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            return await this.SendAsync(request, "default_http", cancellationToken).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string policyName, CancellationToken cancellationToken = default)
        {
            if (this._disposed)
                throw new ObjectDisposedException(nameof(ResilientHttpClient));

            var policy = this._resilienceProvider.GetPolicy<HttpResponseMessage>(policyName);

            return await policy.ExecuteAsync(async ct => await this._httpClient.SendAsync(request, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (!this._disposed)
            {
                this._httpClient.Dispose();
                this._disposed = true;
            }
        }
    }
}
