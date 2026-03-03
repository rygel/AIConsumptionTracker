using AIUsageTracker.Core.Models;

namespace AIUsageTracker.Infrastructure.Configuration;

public static class ConfigNormalization
{
    public static void NormalizeOpenAiCodexSessionOverlap(List<ProviderConfig> configs)
    {
        var openAiConfig = configs.FirstOrDefault(c => c.ProviderId.Equals("openai", StringComparison.OrdinalIgnoreCase));
        if (openAiConfig == null)
        {
            return;
        }

        var hasOpenAiApiKey = !string.IsNullOrWhiteSpace(openAiConfig.ApiKey) &&
                               openAiConfig.ApiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
        if (hasOpenAiApiKey)
        {
            return;
        }

        var codexConfig = configs.FirstOrDefault(c => c.ProviderId.Equals("codex", StringComparison.OrdinalIgnoreCase));
        if (codexConfig == null)
        {
            codexConfig = new ProviderConfig
            {
                ProviderId = "codex",
                Type = ConfigType.Quota,
                PlanType = PlanType.Coding
            };
            configs.Add(codexConfig);
        }

        if (string.IsNullOrWhiteSpace(codexConfig.ApiKey) && !string.IsNullOrWhiteSpace(openAiConfig.ApiKey))
        {
            codexConfig.ApiKey = openAiConfig.ApiKey;
            codexConfig.AuthSource = openAiConfig.AuthSource;
            codexConfig.Description = "Migrated from OpenAI session config";
        }

        configs.RemoveAll(c => c.ProviderId.Equals("openai", StringComparison.OrdinalIgnoreCase));
    }

    public static void NormalizeCodexSparkConfiguration(List<ProviderConfig> configs)
    {
        var sparkConfigs = configs
            .Where(c => c.ProviderId.Equals("codex.spark", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sparkConfigs.Count == 0)
        {
            return;
        }

        var codexConfig = configs.FirstOrDefault(c => c.ProviderId.Equals("codex", StringComparison.OrdinalIgnoreCase));
        if (codexConfig == null)
        {
            codexConfig = new ProviderConfig
            {
                ProviderId = "codex",
                Type = ConfigType.Quota,
                PlanType = PlanType.Coding
            };
            configs.Add(codexConfig);
        }

        foreach (var sparkConfig in sparkConfigs)
        {
            sparkConfig.AuthSource = codexConfig?.AuthSource;
        }
    }
}
