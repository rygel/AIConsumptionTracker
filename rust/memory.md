# Development Memory - Rust AI Consumption Tracker

## Current Work Summary

### Objective
Building a Rust-based cross-platform replacement for the .NET WPF AI Consumption Tracker, with better performance and cross-platform support.

### Architecture
- **aic_core**: Core library with provider implementations and business logic
- **aic_agent**: Background HTTP service (port 8080) that fetches usage data from AI providers
- **aic_app**: Tauri-based desktop application (Windows/Linux/Mac)
- **aic_cli**: Command-line interface
- **aic_web**: Web dashboard for browser access

## Recent Implementations (2026-02-12)

### 1. Info Window (About Dialog)
- Created `info.html` with app and system information
- Shows version, OS details, architecture, machine name, user, config path
- Privacy mode toggle in header
- Synchronized across all windows
- Accessible from tray icon menu

### 2. Privacy Mode Enhancement
- Implemented privacy toggle button (👁/🙈) in all window headers (main, settings, info)
- Hides API keys and sensitive data when enabled
- Cross-window synchronization via Tauri events
- State persistence in localStorage
- Shows reset times and user names as "••••••••" when privacy is on

### 3. Provider Grouping in Main UI
- Grouped providers by payment type in main window:
  - **"Plans & Quotas"** (DeepSkyBlue) - Quota-based and credits providers
  - **"Pay As You Go"** (MediumSeaGreen) - Usage-based providers
- Color-coded group headers with horizontal separators
- Alphabetical sorting within each group

### 4. UI Improvements
- **Dark scrollbar styling**: WebKit and Firefox scrollbars styled to match dark theme
- **Compact mode**: More compact provider bars with reduced padding and sizes
- **Sub-bars for Antigravity**: Shows individual quota usage for each AI model (GPT-4, Claude, etc.)
- **Reset time display**: Shows relative time (e.g., "2d 5h") and absolute time (e.g., "Feb 05 14:30")

### 5. Tray Icon Menu
- Reorganized menu with new items:
  - "Open Settings" 
  - "Info" (About dialog)
  - "Show", "Refresh", "Auto Refresh"
  - "Start/Stop Agent"
  - "Quit"

### 6. Settings Dialog Enhancements
- **Antigravity sub-models**: Shows individual quota checkboxes for each AI model when antigravity is running
- **Escape key**: Pressing Escape closes settings window
- **Alphabetical sorting**: Providers sorted alphabetically by display name
- **Removed grouping**: Settings shows simple alphabetical list (unlike main UI)
- **Updated providers**: Removed kilocode, fixed OpenCode naming

### 7. Agent Improvements
- **Better path resolution**: Searches multiple locations for agent executable:
  - Current directory
  - App resource directory
  - App data directory
  - Development paths (target/debug, target/release)
- **Detailed error messages**: Shows specific guidance when agent fails to start
- **Window drag support**: Added `core:window:allow-start-dragging` permission

### 8. Provider List Updates
- Removed kilocode (not a model provider)
- Fixed OpenCode naming (opencode-zen displays as "OpenCode")
- Removed generic-pay-as-you-go from discovered list
- Changed anthropic provider ID from "anthropic" to "claude-code" to match C# app

### 9. Always-on-Top Persistence
- Saves preference to localStorage
- Applies setting on startup automatically
- Updates checkbox state to match saved preference

### 10. Design Documentation
- Created comprehensive `DESIGN.md` with:
  - Architecture diagrams
  - API endpoints
  - Data models
  - Provider system
  - Configuration management
  - Caching strategy
  - Error handling
  - Build system
  - Development workflow

## Known Issues

1. **Port Conflicts**: Agent sometimes fails to start if port 8080 is in use (need to kill existing processes)
2. **Build Environment**: Occasional Visual Studio/build tool issues on Windows
3. **Window Close Warnings**: Some harmless Win32 errors when closing application
4. **Antigravity Details**: Requires running VS Code extension to show sub-model data

## Next Steps

1. Complete provider implementations for all AI services
2. Add proper error handling for network failures
3. Implement auto-update mechanism
4. Add system tray integration improvements
5. Test cross-platform builds (Linux/Mac)
6. Add click-to-cycle reset display modes (relative/absolute/both)
7. Implement proper window state restoration

## Development Commands

```bash
# Build everything
cd rust && cargo build --release

# Start agent
cd rust && ./target/release/aic_agent.exe

# Build and run UI
cd rust/scripts && ./debug-build.ps1

# Validate HTML/JS
cd rust/aic_app && ./validate.ps1

# Test agent diagnostics
cd rust/scripts && ./test-agent.ps1
```

## Critical Design Principles

### Agent-Only Architecture
**The UI app must NEVER read auth.json or any configuration files directly.** All configuration operations must go through the agent's REST API:

- **Reading providers**: Use `GET /api/providers/discovered`
- **Saving a provider**: Use `PUT /api/providers/{id}`
- **Removing a provider**: Use `DELETE /api/providers/{id}`
- **Saving all providers**: Use `POST /api/config/providers`
- **Triggering discovery**: Use `POST /api/discover`
- **Getting usage**: Use `GET /api/providers/usage`
- **Refreshing usage**: Use `POST /api/providers/usage/refresh`

**Why this matters:**
- Single source of truth: The agent manages all provider configurations
- Consistency: All windows get the same data from the agent
- Security: The agent can implement additional validation and security
- Flexibility: The agent can discover providers from multiple sources (env vars, config files, etc.)

**What was removed:**
- Direct auth.json loading on startup (removed from main.rs setup)
- Direct file access in save/remove provider commands (routed through agent API)
- `ConfigLoader::load_config()` calls in UI (replaced with agent API calls)

**Current State:**
- The agent is the only component that reads/writes auth.json
- The UI app communicates with agent via HTTP on port 8080
- All provider data flows: Agent (source of truth) → UI (display only)

