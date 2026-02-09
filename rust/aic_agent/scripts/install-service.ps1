# AI Consumption Tracker Agent - Windows Service Installation Script
# Run as Administrator

$ServiceName = "aic-agent"
$DisplayName = "AI Consumption Tracker Agent"
$Description = "Background service for collecting AI provider usage data"
$WorkingDir = "$env:LOCALAPPDATA\AI Consumption Tracker"
$ExePath = "$WorkingDir\aic-agent.exe"
$ConfigPath = "$env:APPDATA\ai-consumption-tracker\agent.toml"

function Write-Header {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  AI Consumption Tracker Agent Setup" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-Administrator {
    $currentUser = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-Service {
    Write-Host "[1/4] Installing $ServiceName service..." -ForegroundColor Yellow

    # Create working directory
    if (-not (Test-Path $WorkingDir)) {
        New-Item -ItemType Directory -Path $WorkingDir -Force | Out-Null
    }

    # Create config directory
    $configDir = Split-Path $ConfigPath -Parent
    if (-not (Test-Path $configDir)) {
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    }

    # Create default config if not exists
    if (-not (Test-Path $ConfigPath)) {
        @"
# AI Consumption Tracker Agent Configuration
database_path = "`"$WorkingDir\usage.db`""
listen_address = `"127.0.0.1:8080`"
polling_enabled = true
polling_interval_seconds = 300
log_level = `"info`"
provider_timeout_seconds = 30
retention_days = 30
auto_start = false
"@ | Out-File -FilePath $ConfigPath -Encoding UTF8
        Write-Host "      Created config: $ConfigPath" -ForegroundColor Gray
    }

    # Check if executable exists
    if (-not (Test-Path $ExePath)) {
        Write-Host "      ERROR: aic-agent.exe not found at $ExePath" -ForegroundColor Red
        Write-Host "      Please build the agent first: cargo build --release -p aic_agent" -ForegroundColor Yellow
        return $false
    }

    # Remove existing service if present
    $existing = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "      Removing existing service..." -ForegroundColor Gray
        Stop-Service $ServiceName -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName 2>$null | Out-Null
        Start-Sleep -Seconds 2
    }

    # Install new service
    $result = sc.exe create $ServiceName binPath= "`"$ExePath start`"" DisplayName= "$DisplayName" start= auto obj= "NT AUTHORITY\LocalService" Password= "" 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "      Service installed successfully" -ForegroundColor Green

        # Set service description
        sc.exe description $ServiceName "$Description" 2>&1 | Out-Null

        # Set recovery options
        sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000 2>&1 | Out-Null

        Write-Host ""
        Write-Host "[2/4] Starting service..." -ForegroundColor Yellow
        Start-Service $ServiceName
        Start-Sleep -Seconds 2

        $status = (Get-Service $ServiceName).Status
        if ($status -eq 'Running') {
            Write-Host "      Service is running" -ForegroundColor Green
        } else {
            Write-Host "      WARNING: Service status: $status" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "[3/4] Configuring firewall..." -ForegroundColor Yellow
        # Allow through firewall (optional - localhost only by default)
        # New-NetFirewallRule -DisplayName "$DisplayName" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow -Enabled True 2>$null | Out-Null

        Write-Host ""
        Write-Host "[4/4] Verifying installation..." -ForegroundColor Yellow

        # Test API endpoint
        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:8080/health" -TimeoutSec 5 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                Write-Host "      Agent API is responding at http://127.0.0.1:8080" -ForegroundColor Green
            }
        } catch {
            Write-Host "      WARNING: Agent API not responding yet (may need a moment)" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "  Installation Complete!" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Service: $ServiceName" -ForegroundColor White
        Write-Host "API URL: http://127.0.0.1:8080" -ForegroundColor White
        Write-Host "Config:  $ConfigPath" -ForegroundColor White
        Write-Host ""
        Write-Host "Commands:" -ForegroundColor White
        Write-Host "  Start:   Start-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Stop:    Stop-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Status:  Get-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Logs:    Get-EventLog -Log Application -Source $ServiceName" -ForegroundColor Gray
        Write-Host ""
        return $true
    } else {
        Write-Host "      ERROR: Failed to create service" -ForegroundColor Red
        Write-Host "      $result" -ForegroundColor Red
        return $false
    }
}

function Uninstall-Service {
    Write-Host "[1/3] Uninstalling $ServiceName service..." -ForegroundColor Yellow

    $existing = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        Stop-Service $ServiceName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
        sc.exe delete $ServiceName 2>&1 | Out-Null
        Write-Host "      Service removed" -ForegroundColor Green
    } else {
        Write-Host "      Service not found" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "[2/3] Cleaning up..." -ForegroundColor Yellow
    # Note: We don't remove data files as they may be user data

    Write-Host ""
    Write-Host "[3/3] Done" -ForegroundColor Green
}

# Main
Write-Header

if (-not (Test-Administrator)) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    exit 1
}

$action = $args[0]
if ($action -eq "-uninstall") {
    Uninstall-Service
} else {
    Install-Service
}
