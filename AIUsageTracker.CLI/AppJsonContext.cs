namespace AIUsageTracker.CLI
{
    using System.Text.Json.Serialization;
    using AIUsageTracker.Core.Models;

    [JsonSerializable(typeof(List<ProviderUsage>))]
    [JsonSerializable(typeof(List<ProviderConfig>))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
