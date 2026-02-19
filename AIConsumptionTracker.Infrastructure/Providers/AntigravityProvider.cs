using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using AIConsumptionTracker.Core.Interfaces;
using AIConsumptionTracker.Core.Models;

using AIConsumptionTracker.Infrastructure.Helpers;

namespace AIConsumptionTracker.Infrastructure.Providers;

    public class AntigravityProvider : IProviderService
{
    public string ProviderId => "antigravity";
    private readonly HttpClient _httpClient;
    private readonly ILogger<AntigravityProvider> _logger;
    private ProviderUsage? _cachedUsage;
    private DateTime _cacheTimestamp;
    private List<(int Pid, string Token, int? Port)>? _cachedProcessInfos;
    private DateTime _lastProcessCheck = DateTime.MinValue;

    public AntigravityProvider(ILogger<AntigravityProvider> logger)
    {
        _logger = logger;

        // Setup HttpClient with loose SSL validation for localhost
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
             // Only allow localhost self-signed
             return message.RequestUri?.Host == "127.0.0.1";
        };
        _httpClient = new HttpClient(handler);
    }

    public async Task<IEnumerable<ProviderUsage>> GetUsageAsync(ProviderConfig config, Action<ProviderUsage>? progressCallback = null)
    {
        var results = new List<ProviderUsage>();

        try
        {
            // 1. Find All Processes
            var processInfos = FindProcessInfos();
            if (!processInfos.Any())
            {
                if (_cachedUsage != null)
                {
                    var timeSinceRefresh = DateTime.Now - _cacheTimestamp;
                    var minutesAgo = (int)timeSinceRefresh.TotalMinutes;
                    var description = $"Last refreshed: {minutesAgo}m ago";

                    // Check if any cached reset times have passed and update if so
                    if (_cachedUsage.Details != null && _cachedUsage.Details.Any(d => d.NextResetTime.HasValue))
                    {
                        var anyResetPassed = _cachedUsage.Details
                            .Where(d => d.NextResetTime.HasValue)
                            .Any(d => {
                                var dt = d.NextResetTime!.Value;
                                return dt <= DateTime.Now;
                            });

                        if (anyResetPassed)
                        {
                            _logger.LogDebug("Antigravity reset time passed, showing full bar");
                            // Keep cache but show 0% used (100% remaining) - quota refilled
                            var refilledDetails = _cachedUsage.Details?
                                .Select(d => new ProviderUsageDetail
                                {
                                    Name = d.Name,
                                    Used = "0%",
                                    Description = "Refilled",
                                    NextResetTime = null // Clear reset time since it passed
                                })
                                .ToList();

                            description += " (Quota refilled)";

                            return new[] { new ProviderUsage
                            {
                                ProviderId = ProviderId,
                                ProviderName = "Antigravity",
                                IsAvailable = true,
                                RequestsPercentage = 0,
                                RequestsUsed = 0,
                                RequestsAvailable = _cachedUsage.RequestsAvailable,
                                Details = refilledDetails,
                                AccountName = _cachedUsage.AccountName,
                                Description = description,
                                IsQuotaBased = true,
                                PlanType = PlanType.Coding
                            }};
                        }

                        // No reset passed, show countdown
                        var nextReset = _cachedUsage.Details.FirstOrDefault(d => d.NextResetTime.HasValue)?.NextResetTime;
                        if (nextReset.HasValue)
                        {
                            var timeUntilReset = nextReset.Value - DateTime.Now;
                            if (timeUntilReset.TotalHours > 0)
                            {
                                var hoursUntil = (int)timeUntilReset.TotalHours;
                                var minutesUntil = (int)timeUntilReset.TotalMinutes % 60;
                                description += $" (Resets in {hoursUntil}h {minutesUntil}m)";
                            }
                        }
                    }

                    return new[] { new ProviderUsage
                    {
                        ProviderId = ProviderId,
                        ProviderName = "Antigravity",
                        IsAvailable = true,
                        RequestsPercentage = _cachedUsage.RequestsPercentage,
                        RequestsUsed = _cachedUsage.RequestsUsed,
                        RequestsAvailable = _cachedUsage.RequestsAvailable,
                        Details = _cachedUsage.Details,
                        AccountName = _cachedUsage.AccountName,
                        Description = description,
                        IsQuotaBased = true,
                        PlanType = PlanType.Coding
                    }};
                }
                else
                {
                    var appRunning = IsAntigravityDesktopRunning();
                    return new[] { new ProviderUsage
                    {
                        ProviderId = ProviderId,
                        ProviderName = "Antigravity",
                        IsAvailable = true,
                        RequestsPercentage = 0,
                        RequestsUsed = 0,
                        RequestsAvailable = 0,
                        Description = appRunning
                            ? "Antigravity running, waiting for language server"
                            : "Application not running",
                        IsQuotaBased = true,
                        PlanType = PlanType.Coding
                    }};
                }
            }

            foreach (var info in processInfos)
            {
                try
                {
                    var (pid, csrfToken, commandLinePort) = info;
                    var tokenPreview = csrfToken.Length > 8 ? csrfToken[..8] : csrfToken;
                    _logger.LogDebug("Checking Antigravity process: PID={Pid}, CSRF={Csrf}, PortHint={PortHint}",
                        pid, tokenPreview, commandLinePort?.ToString() ?? "none");

                    // 2. Resolve port (prefer command-line hint, fallback to netstat)
                    var port = commandLinePort ?? FindListeningPort(pid);

                    // 3. Request
                    List<ProviderUsage> usageItems;
                    try
                    {
                        usageItems = await FetchUsage(port, csrfToken, config);
                    }
                    catch when (commandLinePort.HasValue)
                    {
                        var fallbackPort = FindListeningPort(pid);
                        if (fallbackPort == port)
                        {
                            throw;
                        }

                        _logger.LogDebug("Retrying Antigravity PID={Pid} on fallback port {Port}", pid, fallbackPort);
                        usageItems = await FetchUsage(fallbackPort, csrfToken, config);
                    }

                    // Check for duplicates based on AccountName (Email) for the MAIN item
                    // Assuming the first item is the summary
                    var mainItem = usageItems.FirstOrDefault(u => u.ProviderId == ProviderId);
                    
                    if (mainItem != null && results.Any(r => r.ProviderId == ProviderId && r.AccountName == mainItem.AccountName))
                    {
                        continue;
                    }

                    results.AddRange(usageItems);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to check Antigravity PID {info.Pid}");
                }
            }

            if (!results.Any())
            {
                return new[] { new ProviderUsage
                 {
                     ProviderId = ProviderId,
                     ProviderName = "Antigravity",
                     IsAvailable = true,
                     RequestsPercentage = 0,
                     RequestsUsed = 0,
                     RequestsAvailable = 0,
                     Description = "Not running",
                     IsQuotaBased = true,
                     PlanType = PlanType.Coding
                 }};
            }

            // Cache the results for next refresh
            if (results.Any())
            {
                _cachedUsage = results.FirstOrDefault();
                _cacheTimestamp = DateTime.Now;
            }

            // Start with just the summary
            // But we can't just return results list here because we build it differently now
            // The loop above adds to 'results'
            
            // Wait, previous logic was:
            // 1. Find processes
            // 2. Add to results
            // 3. Return results
            
            // New logic inside FetchUsage returns a list (or we need to change FetchUsage signature)
            // Let's change FetchUsage to return IEnumerable<ProviderUsage>
            
            // For now, let's keep FetchUsage returning single and split it here?
            // No, FetchUsage has the context (details list). 
            
            // Refactoring FetchUsage to return List<ProviderUsage>
            
            // ... (See below for FetchUsage refactor)
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Antigravity check failed");
            return new[] { new ProviderUsage
            {
                ProviderId = ProviderId,
                ProviderName = "Antigravity",
                IsAvailable = true,
                RequestsPercentage = 0,
                RequestsUsed = 0,
                RequestsAvailable = 0,
                Description = IsAntigravityDesktopRunning()
                    ? "Antigravity running, waiting for language server"
                    : "Application not running",
                IsQuotaBased = true,
                PlanType = PlanType.Coding
            }};
        }
    }

    private List<(int Pid, string Token, int? Port)> FindProcessInfos()
    {
        if (DateTime.Now - _lastProcessCheck < TimeSpan.FromSeconds(30) && _cachedProcessInfos != null)
        {
            return _cachedProcessInfos;
        }

        var candidates = new List<(int Pid, string Token, int? Port)>();

        if (OperatingSystem.IsWindows())
        {
             try 
             {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name LIKE 'language_server%'");
                using var collection = searcher.Get();

                foreach (var obj in collection)
                {
                    var cmdLine = obj["CommandLine"]?.ToString();
                    var pidVal = obj["ProcessId"];

                    if (string.IsNullOrWhiteSpace(cmdLine) || pidVal == null)
                    {
                        continue;
                    }

                    if (!cmdLine.Contains("antigravity", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var token = ParseCsrfToken(cmdLine);
                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }

                    var pid = Convert.ToInt32(pidVal);
                    var port = ParseExtensionServerPort(cmdLine);
                    candidates.Add((pid, token, port));
                }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Process discovery failed");
             }
        }
        
        candidates = candidates
            .GroupBy(c => c.Pid)
            .Select(g => g.First())
            .ToList();

        _cachedProcessInfos = candidates;
        _lastProcessCheck = DateTime.Now;
        return candidates;
    }

    private static string? ParseCsrfToken(string commandLine)
    {
        var match = Regex.Match(commandLine, @"--csrf[_-]token(?:=|\s+)([a-zA-Z0-9-]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? ParseExtensionServerPort(string commandLine)
    {
        var match = Regex.Match(commandLine, @"--extension_server_port(?:=|\s+)(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
        {
            return port;
        }

        return null;
    }

    private static bool IsAntigravityDesktopRunning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return Process.GetProcessesByName("Antigravity").Any();
        }
        catch
        {
            return false;
        }
    }

    private int FindListeningPort(int pid)
    {
        // ... (existing netstat logic is fine, it uses the specific PID)
        var startInfo = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null) return 0;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var regex = new Regex($@"\s+TCP\s+(?:127\.0\.0\.1|\[::1\]):(\d+)\s+.*LISTENING\s+{pid}");
        
        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
        }
        
        throw new Exception($"No listening port found for PID {pid}");
    }

    private async Task<List<ProviderUsage>> FetchUsage(int port, string csrfToken, ProviderConfig config)
    {
        var results = new List<ProviderUsage>();
        var url = $"https://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/GetUserStatus";
        
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Codeium-Csrf-Token", csrfToken);
        request.Headers.Add("Connect-Protocol-Version", "1");
        
        var body = new { metadata = new { ideName = "antigravity", extensionName = "antigravity", ideVersion = "unknown", locale = "en" } };
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("[Antigravity] Raw response from port {Port}: {Response}", port, responseString[..Math.Min(500, responseString.Length)]);
        
        var data = JsonSerializer.Deserialize<AntigravityResponse>(responseString);
        
        if (data?.UserStatus == null) throw new Exception("Invalid Antigravity response");
        
        _logger.LogDebug("[Antigravity] Email: {Email}, Models: {ModelCount}", 
            PrivacyHelper.MaskContent(data.UserStatus.Email ?? ""), 
            data.UserStatus.CascadeModelConfigData?.ClientModelConfigs?.Count ?? 0);

        var modelConfigs = data.UserStatus.CascadeModelConfigData?.ClientModelConfigs ?? new List<ClientModelConfig>();
        var modelSorts = data.UserStatus.CascadeModelConfigData?.ClientModelSorts ?? new List<ClientModelSort>();
        var labelToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var masterModelLabelSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var masterModelLabels = new List<string>();

        for (var sortIndex = 0; sortIndex < modelSorts.Count; sortIndex++)
        {
            var modelSort = modelSorts[sortIndex];
            var sortName = ResolveModelSortName(modelSort, sortIndex);
            var modelGroups = modelSort.Groups ?? new List<ModelGroup>();
            for (var groupIndex = 0; groupIndex < modelGroups.Count; groupIndex++)
            {
                var groupName = ResolveModelGroupName(modelGroups[groupIndex], sortName, groupIndex);
                foreach (var label in GetModelLabels(modelGroups[groupIndex]))
                {
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    if (masterModelLabelSet.Add(label))
                    {
                        masterModelLabels.Add(label);
                    }

                    if (!labelToGroup.ContainsKey(label))
                    {
                        labelToGroup[label] = groupName;
                    }
                }
            }
        }

        if (!masterModelLabels.Any())
        {
            masterModelLabels = modelConfigs
                .Where(c => !string.IsNullOrWhiteSpace(c.Label))
                .Select(c => c.Label!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var details = new List<ProviderUsageDetail>();
        double minRemaining = 100.0;

        // 1. Check Global Credits
        var planStatus = data.UserStatus.PlanStatus;
        // 1. Check Global Credits - REMOVED per user request

        // Logic removed to hide Flow/Prompt credits
        /* 
        if (planStatus != null) ...
        */

        // 2. Map existing configs for easy lookup
        var configMap = modelConfigs.Where(c => !string.IsNullOrEmpty(c.Label)).ToDictionary(c => c.Label!, c => c);

        // 3. Process all known models (from sorts list) to ensure 0% is shown for exhausted ones
        foreach (var label in masterModelLabels)
        {
            double remainingPct = 0; // Assume exhausted (0% remaining) if missing
            DateTime? itemResetDt = null;
            
            if (configMap.TryGetValue(label, out var modelConfig))
            {
                _logger.LogDebug("[Antigravity] Model {Label}: RemainingFraction={Rem}, TotalRequests={Total}, UsedRequests={Used}",
                    label,
                    modelConfig.QuotaInfo?.RemainingFraction?.ToString() ?? "null",
                    modelConfig.QuotaInfo?.TotalRequests?.ToString() ?? "null",
                    modelConfig.QuotaInfo?.UsedRequests?.ToString() ?? "null");
                
                if (modelConfig.QuotaInfo?.RemainingFraction.HasValue == true)
                {
                    remainingPct = modelConfig.QuotaInfo.RemainingFraction.Value * 100;
                }
                else if (modelConfig.QuotaInfo?.TotalRequests.HasValue == true && modelConfig.QuotaInfo.TotalRequests > 0)
                {
                    var used = modelConfig.QuotaInfo.UsedRequests ?? 0;
                    var remaining = Math.Max(0, modelConfig.QuotaInfo.TotalRequests.Value - used);
                    remainingPct = (remaining / (double)modelConfig.QuotaInfo.TotalRequests.Value) * 100;
                }
            }
            else
            {
                _logger.LogDebug("[Antigravity] Model {Label} not found in config map, defaulting to 0%", label);
            }

            // Store REMAINING percentage for display (consistent with other quota providers)
            // The UI will handle the inverted display logic
            var detailRemainingPct = remainingPct;
            
            string resetStr = "";
            if (!string.IsNullOrEmpty(modelConfig?.QuotaInfo?.ResetTime))
            {
                        if (DateTime.TryParse(modelConfig.QuotaInfo.ResetTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                        {
                            var diff = dt.ToLocalTime() - DateTime.Now;
                    if (diff.TotalSeconds > 0)
                    {
                        resetStr = $" (Resets: ({dt.ToLocalTime():MMM dd HH:mm}))";
                        itemResetDt = dt.ToLocalTime();
                    }
                        }
            }

            var displayModelName = modelConfig?.ModelOrAlias?.Model;
            if (string.IsNullOrWhiteSpace(displayModelName))
            {
                displayModelName = modelConfig?.ModelOrAlias?.Alias;
            }

            if (string.IsNullOrWhiteSpace(displayModelName))
            {
                displayModelName = modelConfig?.ModelOrAlias?.Name;
            }

            if (string.IsNullOrWhiteSpace(displayModelName))
            {
                displayModelName = label;
            }

            details.Add(new ProviderUsageDetail
            {
                Name = label,
                ModelName = displayModelName,
                GroupName = labelToGroup.TryGetValue(label, out var groupName)
                    ? groupName
                    : "Ungrouped Models",
                Used = $"{detailRemainingPct.ToString("F0", CultureInfo.InvariantCulture)}%",
                Description = resetStr,
                NextResetTime = itemResetDt
            });
            
            minRemaining = Math.Min(minRemaining, remainingPct);
        }

        // Sort but keep [Credits] at the top
        // details are already added in order (Credits first, then models by label order from master list which is usually sorted or fixed)
        // If masterModelLabels is not sorted, we might want to sort models alphabetically.
        
        var modelDetails = details.Where(d => !d.Name.StartsWith("[Credits]")).OrderBy(d => d.Name).ToList();
        var creditDetails = details.Where(d => !d.Name.StartsWith("[Credits]")).ToList(); 
        // Wait, logic above separates them.
        
        // Let's just re-sort the whole list to be safe: Credits first, then Alphabetical Models
        var sortedDetails = details.OrderBy(d => d.Name.StartsWith("[Credits]") ? "0" + d.Name : "1" + d.Name).ToList();

        // Show Max Usage (since minRemaining is smallest remaining fraction)
        var usedPctTotal = 100 - minRemaining;
        
        // Remove globalReset based on user feedback. Group-specific resets would be in Details.

        // For quota-based providers: store remaining % in RequestsPercentage, used % in RequestsUsed
        // This matches the pattern used by Z.AI and other quota providers
        // For quota-based providers: store remaining % in RequestsPercentage, used % in RequestsUsed
        // This matches the pattern used by Z.AI and other quota providers
        var remainingPctTotal = minRemaining;
        
        var result = new ProviderUsage
        {
            ProviderId = ProviderId,
            ProviderName = "Antigravity",
            RequestsPercentage = remainingPctTotal,  // Remaining % for quota-based display
            RequestsUsed = 100 - remainingPctTotal,   // Default to percentage
            RequestsAvailable = 100,
            UsageUnit = "Quota %",
            IsQuotaBased = true,
            PlanType = PlanType.Coding,
            Description = $"{remainingPctTotal.ToString("F1", CultureInfo.InvariantCulture)}% Remaining",

            Details = sortedDetails,
            AccountName = data.UserStatus?.Email ?? "",
            NextResetTime = sortedDetails.Where(d => d.NextResetTime.HasValue).OrderBy(d => d.NextResetTime).FirstOrDefault()?.NextResetTime
        };
        
        // Try to find a total limit to expose raw numbers
        // We act as if the sum of all model quotas is the total, OR we pick the largest one.
        // Antigravity is tricky because it has per-model quotas.
        // But usually there's a "global" or "main" quota.
        // Let's check if we have any TotalRequests in the configMap.
        
        long totalLimit = 0;
        long totalUsed = 0;
        bool hasRawNumbers = false;

        // Sum up total requests if available (heuristic)
        foreach(var cfg in configMap.Values)
        {
            if (cfg.QuotaInfo?.TotalRequests.HasValue == true)
            {
                totalLimit += cfg.QuotaInfo.TotalRequests.Value;
                totalUsed += cfg.QuotaInfo.UsedRequests ?? 0;
                hasRawNumbers = true;
            }
        }
        
        if (hasRawNumbers && totalLimit > 0)
        {
            result.RequestsAvailable = totalLimit;
            result.RequestsUsed = totalUsed;
            result.UsageUnit = "Tokens";
            result.DisplayAsFraction = true; // Explicitly request fraction display if we have real numbers
            
            // If we have raw numbers, update description to show it
            // result.Description += $" ({totalUsed}/{totalLimit})";
        }
        
        // Create individual items for each model (child providers)
        foreach (var detail in sortedDetails)
        {
            // Parse percentage from "Used" string (e.g. "80%")
            double detailRemaining = 0;
            if (detail.Used.EndsWith("%") && double.TryParse(detail.Used.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                detailRemaining = parsed;
            }

            var childId = $"{ProviderId}.{detail.Name.ToLowerInvariant().Replace(" ", "-")}";
            // Default name
            var childName = "Antigravity " + detail.Name;
            
            // Check for alias match
            if (config.Models != null && config.Models.Any())
            {
                var match = config.Models.FirstOrDefault(m => 
                    m.Id.Equals(detail.Name, StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Equals(detail.Name, StringComparison.OrdinalIgnoreCase) ||
                    m.Matches.Any(x => x.Equals(detail.Name, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    // Use configured ID if possible, but we need to be careful about changing IDs if users have data.
                    // The user wants "aliases we can use in the database", implying the ID SHOULD change to the alias.
                    // BUT, if we change the ID, old history is lost (detached).
                    // For now, let's keep the ID generation consistent with the old way UNLESS the user explicitly provides an ID in config.
                    // Actually, the user asked for "alias for every provider... in the database".
                    // So we should use match.Id as the suffix if it's a valid ID.
                    
                    if (!string.IsNullOrWhiteSpace(match.Id))
                    {
                        childId = $"{ProviderId}.{match.Id}";
                    }
                    
                    if (!string.IsNullOrWhiteSpace(match.Name))
                    {
                        childName = match.Name;
                    }
                }
            }
            
            // Attempt to find specific config for this model to get raw numbers
            long? detailTotal = null;
            long? detailUsed = null;
            
            if (configMap.TryGetValue(detail.Name, out var cfg) && cfg.QuotaInfo != null)
            {
                detailTotal = cfg.QuotaInfo.TotalRequests;
                detailUsed = cfg.QuotaInfo.UsedRequests;
            }

            var childUsage = new ProviderUsage
            {
                ProviderId = childId,
                ProviderName = childName,
                RequestsPercentage = detailRemaining,
                RequestsUsed = 100 - detailRemaining,
                RequestsAvailable = 100,
                UsageUnit = "Quota %",
                IsQuotaBased = true,
                PlanType = PlanType.Coding,
                Description = $"{detailRemaining.ToString("F0", CultureInfo.InvariantCulture)}% Remaining",
                AccountName = data.UserStatus?.Email ?? "",
                IsAvailable = true,
                FetchedAt = DateTime.UtcNow,
                AuthSource = "antigravity",
                // Inherit raw numbers if available
                DisplayAsFraction = detailTotal.HasValue && detailTotal > 0,
                NextResetTime = detail.NextResetTime
            };

            if (detailTotal.HasValue && detailTotal > 0)
            {
                childUsage.RequestsAvailable = detailTotal.Value;
                childUsage.RequestsUsed = detailUsed ?? 0;
                childUsage.UsageUnit = "Tokens";
            }

            results.Add(childUsage);
        }

        results.Insert(0, result); // Add summary as first item
        
        // Cache the summary result for next refresh (children distinct)
        _cachedUsage = result;
        _cacheTimestamp = DateTime.Now;

        return results;
    }

    private static List<string> GetModelLabels(ModelGroup group)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (group.ModelLabels != null)
        {
            foreach (var label in group.ModelLabels)
            {
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }
        }

        if (group.ExtensionData != null)
        {
            foreach (var key in new[] { "labels", "modelLabels", "models", "items", "model_ids", "modelIds" })
            {
                if (!group.ExtensionData.TryGetValue(key, out var element) || element.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            labels.Add(value);
                        }
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in new[] { "label", "name", "modelLabel", "model_name" })
                        {
                            if (!item.TryGetProperty(property, out var valueElement) || valueElement.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            var value = valueElement.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                labels.Add(value);
                            }
                        }
                    }
                }
            }
        }

        return labels.ToList();
    }

    private static string ResolveModelSortName(ClientModelSort sort, int index)
    {
        if (!string.IsNullOrWhiteSpace(sort.Name))
        {
            return sort.Name;
        }

        if (!string.IsNullOrWhiteSpace(sort.Label))
        {
            return sort.Label;
        }

        if (!string.IsNullOrWhiteSpace(sort.Title))
        {
            return sort.Title;
        }

        if (!string.IsNullOrWhiteSpace(sort.SortId))
        {
            return sort.SortId;
        }

        if (sort.ExtensionData != null)
        {
            foreach (var key in new[] { "name", "label", "title", "id", "sortName", "sort_name" })
            {
                if (sort.ExtensionData.TryGetValue(key, out var element))
                {
                    var value = TryReadStringFromJsonElement(element);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return $"Sort {index + 1}";
    }

    private static string ResolveModelGroupName(ModelGroup group, string sortName, int index)
    {
        if (!string.IsNullOrWhiteSpace(group.Name))
        {
            return group.Name;
        }

        if (!string.IsNullOrWhiteSpace(group.Label))
        {
            return group.Label;
        }

        if (!string.IsNullOrWhiteSpace(group.Title))
        {
            return group.Title;
        }

        if (!string.IsNullOrWhiteSpace(group.GroupName))
        {
            return group.GroupName;
        }

        if (!string.IsNullOrWhiteSpace(group.DisplayName))
        {
            return group.DisplayName;
        }

        if (group.ExtensionData != null)
        {
            foreach (var key in new[] { "name", "label", "title", "groupName", "displayName", "group_label", "group_name" })
            {
                if (group.ExtensionData.TryGetValue(key, out var element))
                {
                    var value = TryReadStringFromJsonElement(element);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return string.IsNullOrWhiteSpace(sortName)
            ? $"Group {index + 1}"
            : $"{sortName} Group {index + 1}";
    }

    private static string? TryReadStringFromJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "name", "label", "title", "displayName" })
            {
                if (element.TryGetProperty(key, out var nested) && nested.ValueKind == JsonValueKind.String)
                {
                    return nested.GetString();
                }
            }
        }

        return null;
    }

    private class AntigravityResponse
    {
        [JsonPropertyName("userStatus")]
        public UserStatus? UserStatus { get; set; }
    }

    private class UserStatus
    {
        [JsonPropertyName("planStatus")]
        public PlanStatus? PlanStatus { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("cascadeModelConfigData")]
        public CascadeModelConfigData? CascadeModelConfigData { get; set; }
    }

    private class PlanStatus
    {
        [JsonPropertyName("availablePromptCredits")]
        public int AvailablePromptCredits { get; set; }
        
        [JsonPropertyName("availableFlowCredits")]
        public int AvailableFlowCredits { get; set; }

        [JsonPropertyName("planInfo")]
        public PlanInfo? PlanInfo { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class PlanInfo
    {
        [JsonPropertyName("monthlyPromptCredits")]
        public int MonthlyPromptCredits { get; set; }

        [JsonPropertyName("monthlyFlowCredits")]
        public int MonthlyFlowCredits { get; set; }
    }

    private class CascadeModelConfigData
    {
        [JsonPropertyName("clientModelConfigs")]
        public List<ClientModelConfig>? ClientModelConfigs { get; set; }

        [JsonPropertyName("clientModelSorts")]
        public List<ClientModelSort>? ClientModelSorts { get; set; }
    }

    private class ClientModelSort
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("id")]
        public string? SortId { get; set; }

        [JsonPropertyName("groups")]
        public List<ModelGroup>? Groups { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class ModelGroup
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("groupName")]
        public string? GroupName { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("modelLabels")]
        public List<string>? ModelLabels { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class ClientModelConfig
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("modelOrAlias")]
        public ModelOrAlias? ModelOrAlias { get; set; }
        
        [JsonPropertyName("quotaInfo")]
        public QuotaInfo? QuotaInfo { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class ModelOrAlias
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("alias")]
        public string? Alias { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class QuotaInfo
    {
        [JsonPropertyName("remainingFraction")]
        public double? RemainingFraction { get; set; }
        
        [JsonPropertyName("totalRequests")]
        public int? TotalRequests { get; set; }
        
        [JsonPropertyName("usedRequests")]
        public int? UsedRequests { get; set; }

        [JsonPropertyName("resetTime")]
        public string? ResetTime { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
