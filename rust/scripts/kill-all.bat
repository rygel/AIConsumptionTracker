@echo off
:: AI Consumption Tracker - Kill All Processes (Batch wrapper)
:: This batch file runs the PowerShell script

echo.
echo Stopping AI Consumption Tracker processes...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0kill-all.ps1" %*

if %errorlevel% neq 0 (
    echo.
    echo Error occurred. Make sure PowerShell is available.
    pause
)
