param(
    [string]$ProjectKey = "AIUsageTracker",
    [switch]$SkipBuild,
    [switch]$SkipCoverage
)

$ErrorActionPreference = "Stop"

function Invoke-DotNetOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args,
        [string]$Step = "dotnet command"
    )

    & dotnet @Args
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

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
$sonarUserHome = Join-Path $repoRoot ".sonar-user-home"
New-Item -ItemType Directory -Path $sonarUserHome -Force | Out-Null

Write-Host "`n--- Beginning analysis ---"
$sonarArgs = @(
    "sonarscanner", "begin",
    "/k:$ProjectKey",
    "/d:sonar.host.url=$hostUrl",
    "/d:sonar.token=$token",
    "/d:sonar.userHome=$sonarUserHome"
)
if (-not $SkipCoverage) {
    $sonarArgs += "/d:sonar.cs.vscoveragexml.reportsPaths=TestResults\coverage.coveragexml"
}
Invoke-DotNetOrThrow -Args $sonarArgs -Step "SonarScanner begin"

if (-not $SkipBuild) {
    Write-Host "`n--- Building solution ---"
    Invoke-DotNetOrThrow -Args @("build", "AIUsageTracker.sln", "--configuration", "Debug") -Step "dotnet build"

    if (-not $SkipCoverage) {
        Write-Host "`n--- Collecting coverage ---"
        & dotnet tool install --global dotnet-coverage 2>$null
        $ErrorActionPreference = "Continue"
        $testDll = Join-Path $repoRoot "AIUsageTracker.Tests\bin\Debug\net8.0-windows10.0.17763.0\AIUsageTracker.Tests.dll"
        dotnet-coverage collect $testDll --output "TestResults\coverage.coverage" --format coverage
        $ErrorActionPreference = "Stop"
        if (Test-Path "TestResults\coverage.coverage") {
            dotnet-coverage convert "TestResults\coverage.coverage" --output "TestResults\coverage.coveragexml" --format coveragexml
            Write-Host "Coverage file generated"
        } else {
            Write-Host "WARNING: No coverage file found"
        }
    }
}

Write-Host "`n--- Ending analysis and uploading ---"
Invoke-DotNetOrThrow -Args @("sonarscanner", "end", "/d:sonar.token=$token") -Step "SonarScanner end"

Write-Host "`nScan complete. View results at $hostUrl/dashboard?id=$ProjectKey"
