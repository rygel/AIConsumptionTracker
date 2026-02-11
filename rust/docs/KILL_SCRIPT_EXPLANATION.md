# How the Kill Script Identifies Processes

## Process Identification Methods

The kill script (`kill-all.ps1`) uses multiple methods to identify AI Consumption Tracker processes:

### 1. Process Name Matching (Primary Method)

Uses PowerShell's `Get-Process -Name` cmdlet which matches the **executable name without the .exe extension**.

```powershell
Get-Process -Name "aic_agent"  # Matches: aic_agent.exe
Get-Process -Name "aic_app"    # Matches: aic_app.exe  
Get-Process -Name "aic-cli"    # Matches: aic-cli.exe
Get-Process -Name "cargo"      # Matches: cargo.exe
```

**Why this works:**
- On Windows, `Get-Process -Name` automatically handles the .exe extension
- It searches for processes where `ProcessName` property matches
- This is the most reliable method for identifying our Rust binaries

### 2. Window Title Matching (Secondary Method)

Checks processes by their main window title for Tauri applications:

```powershell
Get-Process | Where-Object { 
    $_.MainWindowTitle -match "AI Consumption Tracker" 
}
```

**Why this works:**
- Tauri apps set their window title
- Catches the UI even if the process name is different
- Useful when running from cargo (process name might be different)

### 3. Path/Command Line Matching (Tertiary Method)

For Node.js processes that might be running build tools:

```powershell
Get-Process -Name "node" | Where-Object { 
    $_.Path -match "aic" -or $_.CommandLine -match "aic" 
}
```

**Why this works:**
- Node processes often run build scripts
- Checks if the path contains "aic" (AI Consumption Tracker)

## Process Patterns Used

The script defines these patterns to search for:

```powershell
$processPatterns = @(
    @{ Name = "aic_agent"; Display = "AI Agent" },
    @{ Name = "aic_app"; Display = "AI App (UI)" },
    @{ Name = "aic-cli"; Display = "AI CLI" },
    @{ Name = "cargo"; Display = "Cargo Build (optional)" }
)
```

## Why Executable Names?

The script uses executable names (without .exe) because:

1. **Cross-platform compatibility** - Works on both Windows and Unix-like systems
2. **Simplicity** - No need to specify full paths
3. **Reliability** - Process names are stable identifiers
4. **PowerShell convenience** - `Get-Process` handles extensions automatically

## Code Flow

```powershell
# 1. Define what to look for
$processPatterns = @( ... )

# 2. For each pattern, find matching processes
foreach ($pattern in $processPatterns) {
    # Get-Process returns all matching processes
    $processes = Get-Process -Name $pattern.Name
    
    # 3. Kill each found process
    foreach ($proc in $processes) {
        $proc.Kill()
        $proc.WaitForExit(5000)
    }
}

# 4. Also check by window title (catches Tauri apps)
Get-Process | Where-Object { 
    $_.MainWindowTitle -match "AI Consumption Tracker" 
} | ForEach-Object {
    $_.Kill()
}
```

## Edge Cases Handled

1. **Multiple instances** - Script kills ALL matching processes, not just one
2. **Permission errors** - Uses try-catch to handle access denied
3. **Zombie processes** - Waits up to 5 seconds for clean exit
4. **Cargo builds** - Only killed with -Force flag (avoid interrupting other projects)

## Verification

To see what would be killed before actually killing:

```powershell
.\kill-all.ps1 -List
```

This runs the same search logic but only displays found processes without killing them.

## Comparison with Other Methods

| Method | Pros | Cons |
|--------|------|------|
| Process Name | Simple, reliable | Might miss renamed executables |
| Window Title | Catches Tauri apps | Slower, not all apps have titles |
| Path matching | Very specific | Requires knowing install location |
| PID file | Precise | Requires apps to write PID files |

The script uses **Process Name** as primary method because it's the best balance of reliability and simplicity for this use case.
