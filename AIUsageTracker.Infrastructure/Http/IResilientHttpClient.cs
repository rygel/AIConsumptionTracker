namespace AIUsageTracker.Infrastructure.Http
{
    using System.Net;

    public interface IResilientHttpClient
    {
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
    }
}
