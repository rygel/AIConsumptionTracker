#!/usr/bin/env pwsh
# AI Consumption Tracker - Kill All Processes
# Kills all running instances of the agent, UI, and related processes

param(
    [switch]$Force,
    [switch]$List
)

$ErrorActionPreference = 'SilentlyContinue'

function Write-Success { param([string]$msg) Write-Host "✓ $msg" -ForegroundColor Green }
function Write-Info { param([string]$msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warn { param([string]$msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }

Write-Host "`n🛑 AI Consumption Tracker - Process Killer`n" -ForegroundColor Red

# Define process patterns to kill
$processPatterns = @(
    @{ Name = "aic_agent"; Display = "AI Agent" },
    @{ Name = "aic_app"; Display = "AI App (UI)" },
    @{ Name = "aic-cli"; Display = "AI CLI" },
    @{ Name = "cargo"; Display = "Cargo Build (optional)" }
)

# List running processes
if ($List) {
    Write-Info "Listing running processes...`n"
    $found = $false
    
    foreach ($pattern in $processPatterns) {
        $processes = Get-Process -Name $pattern.Name -ErrorAction SilentlyContinue
        if ($processes) {
            $found = $true
            Write-Host "$($pattern.Display):" -ForegroundColor Yellow
            foreach ($proc in $processes) {
                Write-Host "  PID: $($proc.Id), Started: $($proc.StartTime), CPU: $($proc.CPU)" -ForegroundColor Gray
            }
        }
    }
    
    if (-not $found) {
        Write-Host "No AI Consumption Tracker processes found running." -ForegroundColor Green
    }
    
    Write-Host ""
    exit 0
}

# Kill processes
$totalKilled = 0

foreach ($pattern in $processPatterns) {
    $processes = Get-Process -Name $pattern.Name -ErrorAction SilentlyContinue
    
    if ($processes) {
        foreach ($proc in $processes) {
            try {
                if ($Force -or $pattern.Name -ne "cargo") {
                    $proc.Kill()
                    $proc.WaitForExit(5000) | Out-Null
                    Write-Success "Killed $($pattern.Display) (PID: $($proc.Id))"
                    $totalKilled++
                } else {
                    Write-Warn "Skipped $($pattern.Display) (PID: $($proc.Id)) - use -Force to kill cargo"
                }
            } catch {
                Write-Warn "Failed to kill $($pattern.Display) (PID: $($proc.Id)): $_"
            }
        }
    }
}

# Note: Window title matching removed because it's too broad
# (could match Discord, browsers, etc. with "AI Consumption Tracker" in title)
# Only process name matching is used for safety

# Optional: Check for Node.js processes related to this project (only with -Force)
if ($Force) {
    Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object { 
        # Check if running from our project directory
        ($_.Path -and $_.Path -match "rust[\\/]aic") -or
        ($_.CommandLine -and $_.CommandLine -match "rust[\\/]aic")
    } | ForEach-Object {
        try {
            $_.Kill()
            Write-Success "Killed Node process (PID: $($_.Id))"
            $totalKilled++
        } catch {
            Write-Warn "Failed to kill Node process (PID: $($_.Id)): $_"
        }
    }
}

Write-Host ""
if ($totalKilled -gt 0) {
    Write-Success "Killed $totalKilled process(es)"
} else {
    Write-Host "No AI Consumption Tracker processes were running." -ForegroundColor Green
}

Write-Host "`nDone.`n"
