param(
    [string]$ProjectKey = "AIUsageTracker",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$envFile = Join-Path $repoRoot ".env"

if (Test-Path $envFile) {
    Write-Host "Loading environment from .env..."
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#=]+)=(.*)$') {
            $name = $Matches[1].Trim()
            $value = $Matches[2].Trim()
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
}

$token = $env:SONAR_TOKEN
$hostUrl = $env:SONAR_HOST_URL

if ([string]::IsNullOrEmpty($token)) {
    Write-Error "SONAR_TOKEN not set. Copy .env.example to .env and fill in your credentials."
    exit 1
}

if ([string]::IsNullOrEmpty($hostUrl)) {
    $hostUrl = "http://localhost:9000"
}

Write-Host "Starting SonarQube scan for project: $ProjectKey"
Write-Host "Server: $hostUrl"

Set-Location $repoRoot

Write-Host "`n--- Beginning analysis ---"
dotnet sonarscanner begin /k:"$ProjectKey" /d:sonar.host.url="$hostUrl" /d:sonar.token="$token"

if (-not $SkipBuild) {
    Write-Host "`n--- Building solution ---"
    dotnet build AIUsageTracker.sln --configuration Debug
}

Write-Host "`n--- Ending analysis and uploading ---"
dotnet sonarscanner end /d:sonar.token="$token"

Write-Host "`nScan complete. View results at $hostUrl/dashboard?id=$ProjectKey"
