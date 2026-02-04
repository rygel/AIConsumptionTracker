# AI Consumption Tracker - Distribution Packaging Script
# Usage: .\scripts\publish-app.ps1

$projectName = "AIConsumptionTracker.UI"
$projectPath = ".\AIConsumptionTracker.UI\AIConsumptionTracker.UI.csproj"
$publishDir = ".\dist\publish"
$zipPath = ".\dist\AIConsumptionTracker.zip"

Write-Host "Cleaning dist folder..." -ForegroundColor Cyan
if (Test-Path ".\dist") { Remove-Item -Recurse -Force ".\dist" }
New-Item -ItemType Directory -Path $publishDir

Write-Host "Publishing $projectName..." -ForegroundColor Cyan
# Options:
# -c Release: Build in release mode
# -r win-x64: Target Windows 64-bit
# --self-contained true: Include .NET runtime
# -p:PublishSingleFile=true: Bundle into one exe
# -p:PublishReadyToRun=true: Optimize for startup speed
# -p:DebugType=None: Remove debug info
dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=None

Write-Host "Verifying output..." -ForegroundColor Cyan
if (Test-Path "$publishDir\$projectName.exe") {
    Write-Host "Build Successful: $projectName.exe created." -ForegroundColor Green
} else {
    Write-Host "Build Failed: $projectName.exe not found in $publishDir." -ForegroundColor Red
    exit 1
}

Write-Host "Creating Distribution ZIP..." -ForegroundColor Cyan
# We compress the whole publish directory
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

Write-Host "--------------------------------------------------" -ForegroundColor Yellow
Write-Host "Distribution ready at: $zipPath" -ForegroundColor Green
Write-Host "Size: $((Get-Item $zipPath).Length / 1MB) MB" -ForegroundColor Gray
Write-Host "--------------------------------------------------" -ForegroundColor Yellow

