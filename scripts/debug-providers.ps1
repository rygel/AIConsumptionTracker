param(
    [string[]]$Providers = @(),
    [string]$OutputDir = "test-fixtures"
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyy-MM-ddTHH-mm-ss"
$OutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $PSScriptRoot "..\$OutputDir" }

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

function Get-CodexToken {
    $authPath = Join-Path $env:USERPROFILE ".codex\auth.json"
    if (-not (Test-Path $authPath)) {
        Write-Host "[Codex] Auth file not found: $authPath" -ForegroundColor Yellow
        return $null
    }
    
    $authJson = Get-Content $authPath -Raw | ConvertFrom-Json
    if ($authJson.tokens -and $authJson.tokens.access_token) {
        return $authJson.tokens.access_token
    }
    
    Write-Host "[Codex] No access token found in auth file" -ForegroundColor Yellow
    return $null
}

function Get-TrackerApiKeys {
    $authPath = Join-Path $env:USERPROFILE ".ai-consumption-tracker\auth.json"
    if (-not (Test-Path $authPath)) {
        return @{}
    }
    
    $authJson = Get-Content $authPath -Raw | ConvertFrom-Json
    $keys = @{}
    
    foreach ($provider in $authJson.PSObject.Properties) {
        if ($provider.Value -is [System.Management.Automation.PSCustomObject] -and $provider.Value.PSObject.Properties.Name -contains "key") {
            $key = $provider.Value.key
            if ($key) {
                $keys[$provider.Name] = $key
            }
        }
    }
    
    return $keys
}

$TrackerApiKeys = Get-TrackerApiKeys

function Invoke-ProviderRequest {
    param(
        [string]$Name,
        [string]$ApiKey,
        [string]$Endpoint,
        [hashtable]$Headers = @{},
        [string]$Method = "GET"
    )
    
    try {
        Write-Host "[$Name] Fetching from $Endpoint..." -ForegroundColor Cyan
        
        $params = @{
            Uri = $Endpoint
            Method = $Method
            Headers = $Headers
            ErrorAction = "Stop"
        }
        
        $response = Invoke-RestMethod @params
        
        $filename = Join-Path $OutputDir "$($Name.ToLower())-$timestamp.json"
        $response | ConvertTo-Json -Depth 15 | Out-File -FilePath $filename -Encoding UTF8
        
        Write-Host "[$Name] Saved to $filename" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "[$Name] Error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# For providers where we don't have env vars, try to read from Monitor's running API
# This requires the Monitor to be running and have fetched data recently

function Get-FromMonitorApi {
    param([string]$ProviderId)
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5000/api/usage/$ProviderId" -ErrorAction SilentlyContinue
        if ($response -and $response.provider_id) {
            return $response
        }
    }
    catch {
        # Ignore
    }
    return $null
}

$availableProviders = @{
    "codex" = {
        $token = Get-CodexToken
        if ($token) {
            Invoke-ProviderRequest -Name "Codex" -ApiKey $token -Endpoint "https://chatgpt.com/backend-api/wham/usage" -Headers @{ "Authorization" = "Bearer $token" }
        }
        else { Write-Host "[Codex] No token found" -ForegroundColor Yellow }
    }
    
    "kimi" = {
        $apiKey = $env:KIMI_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("kimi")) { $apiKey = $TrackerApiKeys["kimi"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "Kimi" -ApiKey $apiKey -Endpoint "https://api.kimi.com/coding/v1/usages" -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[Kimi] No API key found" -ForegroundColor Yellow }
    }
    
    "anthropic" = {
        $apiKey = $env:ANTHROPIC_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("anthropic")) { $apiKey = $TrackerApiKeys["anthropic"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "Anthropic" -ApiKey $apiKey -Endpoint "https://api.anthropic.com/v1/usage" -Headers @{ "x-api-key" = $apiKey; "anthropic-version" = "2023-06-01" }
        }
        else { Write-Host "[Anthropic] No API key found" -ForegroundColor Yellow }
    }
    
    "openai" = {
        $apiKey = $env:OPENAI_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("openai")) { $apiKey = $TrackerApiKeys["openai"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "OpenAI" -ApiKey $apiKey -Endpoint "https://api.openai.com/v1/usage" -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[OpenAI] No API key found" -ForegroundColor Yellow }
    }
    
    "openrouter" = {
        $apiKey = $env:OPENROUTER_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("openrouter")) { $apiKey = $TrackerApiKeys["openrouter"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "OpenRouter" -ApiKey $apiKey -Endpoint "https://openrouter.ai/api/v1/credits" -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[OpenRouter] No API key found" -ForegroundColor Yellow }
    }
    
    "mistral" = {
        $apiKey = $env:MISTRAL_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("mistral")) { $apiKey = $TrackerApiKeys["mistral"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "Mistral" -ApiKey $apiKey -Endpoint "https://api.mistral.ai/v1/me" -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[Mistral] No API key found" -ForegroundColor Yellow }
    }
    
    "deepseek" = {
        $apiKey = $env:DEEPSEEK_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("deepseek")) { $apiKey = $TrackerApiKeys["deepseek"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "DeepSeek" -ApiKey $apiKey -Endpoint "https://api.deepseek.com/user/balance" -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[DeepSeek] No API key found" -ForegroundColor Yellow }
    }
    
    "zai" = {
        $apiKey = $env:ZAI_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("zai-coding-plan")) { $apiKey = $TrackerApiKeys["zai-coding-plan"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "Zai" -ApiKey $apiKey -Endpoint "https://api.z.ai/api/monitor/usage/quota/limit" -Headers @{ "Authorization" = $apiKey; "Accept-Language" = "en-US,en" }
        }
        else { Write-Host "[Zai] No API key found" -ForegroundColor Yellow }
    }
    
    "xiaomi" = {
        $apiKey = $env:XIAOMI_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("xiaomi")) { $apiKey = $TrackerApiKeys["xiaomi"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "Xiaomi" -ApiKey $apiKey -Endpoint "https://api.xiaomimimo.com/v1/user/balance" -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[Xiaomi] No API key found" -ForegroundColor Yellow }
    }
    
    "synthetic" = {
        $apiKey = $env:SYNTHETIC_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("synthetic")) { $apiKey = $TrackerApiKeys["synthetic"] }
        if ($apiKey) {
            $endpoint = "https://account.synthetic.ai/api/usage"
            Invoke-ProviderRequest -Name "Synthetic" -ApiKey $apiKey -Endpoint $endpoint -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else {
            $monitorData = Get-FromMonitorApi -ProviderId "synthetic"
            if ($monitorData) {
                $filename = Join-Path $OutputDir "synthetic-$timestamp.json"
                $monitorData | ConvertTo-Json -Depth 10 | Out-File -FilePath $filename -Encoding UTF8
                Write-Host "[Synthetic] Saved from Monitor cache to $filename" -ForegroundColor Green
            }
            else { Write-Host "[Synthetic] No API key or Monitor not available" -ForegroundColor Yellow }
        }
    }
    
    "opencode" = {
        $apiKey = $env:OPENCODE_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("opencode")) { $apiKey = $TrackerApiKeys["opencode"] }
        if ($apiKey) {
            $endpoint = "https://opencode.ai/api/usage"
            Invoke-ProviderRequest -Name "OpenCode" -ApiKey $apiKey -Endpoint $endpoint -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[OpenCode] No API key found" -ForegroundColor Yellow }
    }
    
    "minimax" = {
        $apiKey = $env:MINIMAX_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("minimax")) { $apiKey = $TrackerApiKeys["minimax"] }
        if ($apiKey) {
            $endpoint = "https://api.minimax.chat/v1/user/balance"
            Invoke-ProviderRequest -Name "Minimax" -ApiKey $apiKey -Endpoint $endpoint -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[Minimax] No API key found" -ForegroundColor Yellow }
    }
    
    "github-copilot" = {
        $apiKey = $env:GITHUB_TOKEN
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("github-copilot")) { $apiKey = $TrackerApiKeys["github-copilot"] }
        if ($apiKey) {
            $endpoint = "https://api.github.com/copilot_internal/usage"
            Invoke-ProviderRequest -Name "GitHubCopilot" -ApiKey $apiKey -Endpoint $endpoint -Headers @{ "Authorization" = "Bearer $apiKey"; "Accept" = "application/vnd.github.copilot-internal+json" }
        }
        else { Write-Host "[GitHub Copilot] No API key found" -ForegroundColor Yellow }
    }
    
    "claude-code" = {
        $apiKey = $env:ANTHROPIC_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("claude-code")) { $apiKey = $TrackerApiKeys["claude-code"] }
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("anthropic")) { $apiKey = $TrackerApiKeys["anthropic"] }
        if ($apiKey) {
            Invoke-ProviderRequest -Name "ClaudeCode" -ApiKey $apiKey -Endpoint "https://api.anthropic.com/v1/claude_code_usage" -Headers @{ "x-api-key" = $apiKey; "anthropic-version" = "2023-06-01" }
        }
        else { Write-Host "[ClaudeCode] No API key found" -ForegroundColor Yellow }
    }
    
    "antigravity" = {
        $apiKey = $env:ANTIGRAVITY_API_KEY
        if (-not $apiKey -and $TrackerApiKeys.ContainsKey("antigravity")) { $apiKey = $TrackerApiKeys["antigravity"] }
        if ($apiKey) {
            $endpoint = "https://antigravity.dev/api/usage"
            Invoke-ProviderRequest -Name "Antigravity" -ApiKey $apiKey -Endpoint $endpoint -Headers @{ "Authorization" = "Bearer $apiKey" }
        }
        else { Write-Host "[Antigravity] No API key found" -ForegroundColor Yellow }
    }
}

Write-Host "=== AI Provider API Debug Tool ===" -ForegroundColor Magenta
Write-Host "Timestamp: $timestamp" -ForegroundColor Gray
Write-Host "Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

if ($Providers.Count -eq 0) {
    Write-Host "Fetching all available providers..." -ForegroundColor Cyan
    $Providers = $availableProviders.Keys | Sort-Object
}
else {
    Write-Host "Fetching providers: $($Providers -join ', ')" -ForegroundColor Cyan
}

$success = 0
$failed = 0

foreach ($provider in $Providers) {
    $providerLower = $provider.ToLower()
    if ($availableProviders.ContainsKey($providerLower)) {
        Write-Host ""
        & $availableProviders[$providerLower]
        $success++
    }
    else {
        Write-Host "[$provider] Unknown provider" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Magenta
Write-Host "Success: $success" -ForegroundColor Green
Write-Host "Failed/Not configured: $failed" -ForegroundColor $(if ($failed -gt 0) { "Yellow" } else { "Green" })
