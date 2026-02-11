# AI Token Tracker - Quick Debug Script
# Simple PowerShell script for fast development builds

param(
    [switch]$Help,
    [switch]$RunOnly
)

# Color output functions
function Write-Success { param([string]$msg) Write-Host "✓ $msg" -ForegroundColor Green }
function Write-Error { param([string]$msg) Write-Host "❌ $msg" -ForegroundColor Red }
function Write-Info { param([string]$msg) Write-Host "ℹ️  $msg" -ForegroundColor Blue }

if ($Help) {
    Write-Host "AI Token Tracker - Quick Debug Script"
    Write-Host "Usage: .\debug-build.ps1 [OPTIONS]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -RunOnly    Skip build, just run existing build"
    Write-Host "  -Help       Show this help"
    exit 0
}

Write-Host "`n🚀 AI Token Tracker - Quick Debug`n"

# Check directory
if (-not (Test-Path "aic_app\Cargo.toml")) {
    Write-Error "Must run from rust/ directory"
    Write-Info "Current: $(Get-Location)"
    Write-Info "Use: cd rust; .\debug-build.ps1"
    exit 1
}

# Check dependencies
try { cargo --version | Out-Null; Write-Success "Rust found" } 
catch { Write-Error "Rust not found - install from https://rustup.rs/"; exit 1 }

try { node --version | Out-Null; Write-Success "Node.js found" } 
catch { Write-Error "Node.js not found - install from https://nodejs.org/"; exit 1 }

# Build or run
Set-Location "aic_app"

if (-not $RunOnly) {
    Write-Info "Building debug version..."
    cargo tauri build --no-bundle
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Build failed"
        exit 1 
    }
    Write-Success "Build complete"
}

Write-Info "Starting application..."
Write-Host ""
cargo run

Write-Host "`nApplication closed."