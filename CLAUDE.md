# CLAUDE.md

## Build & Test

Before building, kill any running app/monitor instances that lock DLLs:

```powershell
pwsh -File scripts/kill-all.ps1
```

Run tests (capped at 4 cores per global CLAUDE.md):

```bash
dotnet test AIUsageTracker.Tests/AIUsageTracker.Tests.csproj -T 4
```

## Architecture

- **AIUsageTracker.UI.Slim** — WPF main window + settings dialog (net8.0-windows)
- **AIUsageTracker.Monitor** — Background agent that polls providers and serves usage data over HTTP
- **AIUsageTracker.Core** — Shared models, interfaces, monitor client
- **AIUsageTracker.Infrastructure** — Provider implementations (Synthetic, Codex, OpenAI, etc.)

### Data flow: Monitor → Main Window

1. Monitor polls each configured provider via `IProvider.GetUsageAsync(config)`
2. Results are grouped into `AgentGroupedUsageSnapshot` and served via HTTP with ETag caching
3. Main window polls `MonitorService.GetGroupedUsageAsync()` every 2-60 seconds
4. `GroupedUsageDisplayAdapter.Expand()` flattens the snapshot into `List<ProviderUsage>`
5. `MainWindowRuntimeLogic.PrepareForMainWindow()` filters by visibility and state
6. `RenderProviders()` builds the card UI

### Settings dialog interaction

- Settings loads its own copy of `_configs` and `_usages` from the monitor
- Config changes are auto-saved with 600ms debounce via `PersistAllSettingsAsync`
- Settings always shows all default providers (ShowInSettings=true) as configuration slots
- On close, `DialogResult = true` triggers main window to call `InitializeAsync()` which re-fetches everything
- After config saves/removals, `MonitorService.InvalidateGroupedUsageCache()` is called to prevent stale ETag responses

### Provider settings modes

- **StandardApiKey** — User-editable API key field (Synthetic, Mistral, Kimi, etc.)
- **SessionAuthStatus** — Session-based auth with status display (Codex, OpenAI)
- **AutoDetectedStatus** — Auto-discovered, read-only (Antigravity, OpenCode Zen)
- **ExternalAuthStatus** — External auth flow (GitHub Copilot)

Only StandardApiKey providers can have their keys deleted by the user.
