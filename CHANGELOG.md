# Changelog

## [Unreleased]

## [2.3.3] - 2026-03-22

### Added
- **3-tier pace badges with projected usage**: Headroom (green, projected <70%), On pace (green, 70–100%), Over pace (red, >100%). Shows projected end-of-period percentage (e.g. "Projected: 73%") next to each badge.
- **Card Designer** in Settings → Cards: configurable card slots, presets, compact mode with background progress bar.
- **Auto-collapse inactive providers**: providers with 0% usage grouped into expandable "Other providers" section.
- **Configurable reset time format**: absolute by default, relative optional via right-click or Settings.
- **Daily budget in tooltip**: shows daily budget and expected vs actual usage for weekly providers.
- **Per-window pace projection**: burst and weekly windows independently pace-projected.

### Performance
- **Startup ~19s → ~3.5s**: monitor launch now runs in parallel with WPF initialization. Fast-fail health checks (1.5s timeout), eliminated redundant HTTP timeouts and double preferences load.

### Fixed
- **Pace calculation completely reworked**: replaced cubic suppression formula with simple linear projection (`projected = used / elapsed_fraction`). Works correctly for all window sizes.
- **Codex reset badge showed 3 parts**: Spark's reset time leaked into the parent card. Now correctly shows only burst + weekly.
- **DateTime overflow on stale provider data**: three separate copies of pace logic each had the same underflow bug. Consolidated all pace math into `UsageMath` — single source of truth.
- **Empty UI on startup**: MonitorLauncher DI resolution failed; added DI-friendly constructor.
- **ConfigureAwait(false) deadlock**: removed from UI code path; added guardrail test.
- **Pace used burst reset time instead of weekly**: parent card's NextResetTime now correctly uses the rolling window.
- **Unthemed dialogs**: update progress window and save preset dialog now themed on dark mode.
- Re-enabled 7 dangerous analyzer rules that were globally suppressed. Fixed all violations.

### Changed
- **~7,000 lines removed**: dead code, unused abstractions, duplicate interfaces, Polly resilience stack.
- **Settings redesigned**: new Cards tab, Layout tab slimmed, Data merged into Monitor, two-column layouts.
- Split SettingsWindow.xaml.cs into partial classes per tab.
- Replace reflection-based provider discovery with explicit static registration.
- Convert MonitorLauncher from static to injectable singleton.
- Card Designer moved from separate window to inline Settings Cards tab.
- Dark SVG icons get white glow on dark themes.
- Consolidated duplicated helpers into UsageMath, PrivacyHelper, UIHelper.

### Security
- Fixed path traversal vulnerability in Seeder script.
- Added gitleaks baseline for known false positives; push scan now uses baseline.

### CI/CD
- Security scan now weekly-only. Removed fake build-performance-monitor workflow.
- NuGet caching added to publish, dependency-updates, and security-scan workflows.
- Playwright browser caching for web tests.
- Screenshot baselines auto-commit with [skip ci].

## [2.3.2-beta.7] - 2026-03-20

### Fixed
- Pace-adjusted quota colours now apply correctly to model-specific detail cards (for example Claude Sonnet/Opus) by propagating declared period durations through the catalog pipeline.
- Legacy database compatibility bootstrap now ensures `provider_history.next_reset_time` exists before timestamp conversion, preventing migration failures on older schemas.
- Web usage/history mapping now handles both epoch and text timestamps consistently, restoring correct reliability and latest-row projections.

### Changed
- Removed fallback heuristics in quota/reset presentation paths; dual-bucket and reset badge rendering now resolve from provider metadata declarations.
- Simplified percentage parsing to explicit supported formats while preserving plain numeric values (for example `"50"`).
- Normalized formatting/newline/encoding and analyzer baseline configuration so release pre-commit validation passes cleanly.

### CI/CD
- **NuGet caching added to `publish` workflow**: All 6 matrix jobs (win-x64/x86/arm64, linux-x64, osx-x64/arm64) now share a NuGet package cache, eliminating redundant restores on every publish run.
- **Playwright browser caching**: Chromium (~300MB) is now cached in `web-tests-windows` keyed on the Web.Tests project file — skipped on cache hit.
- **Cross-platform test job now uses NuGet cache**: The Ubuntu core-test job was the only job without NuGet caching.
- **CodeQL runs on push to main/develop**: Previously weekly only; now also triggered on source code changes post-merge.
- **OSSF Scorecard**: Added supply-chain security scoring, publishes results to securityscorecards.dev and GitHub Code Scanning.
- **Gitleaks secret scanning**: Added secret scanning on every PR and push; scans only PR-introduced commits on pull requests, full history on push.
- **GitHub Actions updated**: trivy-action v0.29.0→v0.35.0, scorecard-action v2.4.0→v2.4.3, codeql-action/upload-sarif v3→v4.34.0 (consistent across all workflows), actions/cache v5.0.3→v5.0.4.
- **Resolved zizmor template-injection alerts**: `publish.yml` and `pr-size-check.yml` `${{ }}` expressions moved from inline shell/JS into `env:` vars.

## [2.3.2-beta.6] - 2026-03-20

### Fixed
- **Pace-adjusted colour not applied to Sonnet card**: `ResolveRollingWindowInfo()` path 2 searched only for `WindowKind.Rolling` details — which excluded the Sonnet card (`WindowKind.ModelSpecific`) — so `ColorIndicatorPercent` fell back to the raw used percentage and rendered yellow despite the user being well under pace.

### Refactored
- **Removed `ResolveRollingWindowInfo()` fallback chain**: `ProviderUsageDisplayCatalog.ExpandSyntheticAggregateChildren` now calls `EnrichWithPeriodDuration` on every non-aggregate usage before yielding it, setting `PeriodDuration` directly from the catalog's primary rolling window definition. `ProviderCardViewModel.ColorIndicatorPercent` and `PaceBadgeText` now read `Usage.PeriodDuration` and `Usage.NextResetTime` directly — one condition, no catalog lookup, no detail iteration.
- **`CreateAggregateDetailUsage` uses `detail.NextResetTime ?? parentUsage.NextResetTime`**: ensures the child card always has a reset time for pace calculation even when the provider detail's own reset time is absent.

## [2.3.2-beta.5] - 2026-03-20

### Fixed
- **Pace-adjusted bar still yellow when clearly under pace**: The quadratic forgiveness formula (`usedPercent² / expectedPercent`) produced a pace-adjusted value of ~60.2% for Sonnet at 73% used with 88.5% of the 7-day window elapsed — barely above the 60% yellow threshold despite being well under pace. Changed to a cubic formula (`usedPercent³ / expectedPercent²`) which gives 49.7% in the same scenario, correctly rendering the bar green. The new formula is more forgiving for providers that are meaningfully under pace while still warning when genuinely approaching the limit.

## [2.3.2-beta.4] - 2026-03-20

### Fixed
- **Quota-based provider cards permanently stuck yellow/red**: `PercentageToColorConverter` was computing `remaining = 100 − usedPercent` for quota-based providers and then checking `remaining ≥ yellowThreshold`. With default thresholds (yellow = 60, red = 80), any provider with more than 40% remaining was shown as yellow or red regardless of actual usage — e.g. 30% used → 70% remaining → 70 ≥ 60 → yellow. The fix removes the inversion: colour thresholds now always compare against the used percentage (pace-adjusted for rolling-window providers), consistent with the threshold labels in Settings. Affects all quota-based providers: Claude Code, GitHub Copilot, OpenAI Codex, Z.AI, Opencode Zen, and others.

### Security
- **Resolved all 64 GitHub code-scanning alerts** (zizmor): `dangerous-triggers` — added trusted-source repository guard to the `workflow_run` trigger in `build-performance-monitor.yml`; `artipacked` — added `persist-credentials: false` to checkout steps in `release.yml` and `dependency-updates.yml`; `excessive-permissions` — added `permissions: contents: read` at workflow level in `experimental-rust.yml`; `template-injection` — moved all inline `${{ }}` expressions in shell scripts and `github-script` blocks to `env:` vars across `tests.yml`, `build-performance-monitor.yml`, and `dependency-updates.yml`.

## [2.3.2-beta.3] - 2026-03-20

### Fixed
- **Pace adjustment not applied to Claude Code Sonnet/Opus cards**: The rolling-window pace indicator was correctly applied to the primary Claude Code card but not to the per-model Sonnet and Opus detail cards. All three cards now reflect pace-adjusted colours and thresholds consistently.

### Refactored
- **Provider metadata moved to `ProviderDefinition` (single source of truth)**: Static characteristics (`IsStatusOnly`, `IsCurrencyUsage`, `DisplayAsFraction`, `PlanType`, `IsQuotaBased`, `ProviderName`) are now declared once on `ProviderDefinition`. Per-instance assignments on `ProviderUsage` have been removed. The processing pipeline enforces definition values at the boundary — `PlanType` and `IsQuotaBased` are now overridden from the definition on every normalisation pass, not just at provider construction time.
- **`ProviderBase.CreateUnavailableUsage` sources metadata from definition**: Previously defaulted to `PlanType.Coding` and `IsQuotaBased = true` for all error paths, producing incorrect metadata on error cards for providers like Mistral and DeepSeek.
- **Eliminated post-fetch filtering in GeminiProvider**: Error results from failed account fetches are no longer added to the results list and then filtered out; only successful fetches enter the list. If all accounts fail a single unavailable card is returned.
- **GeminiProvider OAuth client retry made explicit**: The implicit `catch when clientId == CLI` silent retry is replaced by `ResolveOAuthClientsToTry()` + `DetectPreferredOAuthClientId()` — the fallback order is now declared upfront.
- **`ProviderUsageDisplayCatalog` single-pass filtering**: The two sequential filter passes (`ShouldShowInMainWindow` + hidden items) are merged into one `.Where()` predicate.
- **`ResolveDisplayLabel` simplified**: Collapsed from a 7-level fallback chain to 3 clear branches with documented precedence.

## [2.3.2-beta.2] - 2026-03-20

### Fixed
- **Pace adjustment not applied to Claude Code Sonnet/Opus cards**: The rolling-window pace indicator was correctly applied to the primary Claude Code card but not to the per-model Sonnet and Opus detail cards. All three cards now reflect pace-adjusted colours and thresholds consistently.

### Refactored
- **Provider metadata moved to `ProviderDefinition`**: Static characteristics (`IsStatusOnly`, `IsCurrencyUsage`, `DisplayAsFraction`, `PlanType`, `IsQuotaBased`, `ProviderName`) are now declared once on `ProviderDefinition` and sourced from there at runtime. Per-instance assignments on `ProviderUsage` objects have been removed. The processing pipeline ORs in definition flags so that any provider declaring a flag gets it applied even if the runtime object omits it.
- **`ProviderBase.CreateUnavailableUsage` now sources metadata from definition**: The helper previously defaulted to `PlanType.Coding` and `IsQuotaBased = true` for all error/unavailable paths regardless of the actual provider type, causing incorrect metadata on error cards for providers like Mistral and DeepSeek.

## [2.3.2-beta.1] - 2026-03-20

### Added
- **Pace-aware quota colours**: For providers with rolling quota windows (Claude Code 7-day, GitHub Copilot weekly, OpenAI/Codex weekly), the progress-bar colour and notification threshold now account for how much of the quota period has elapsed. A provider at 70% usage with only one day left of a 7-day window is considered on-budget and stays green — not yellow or red — because consumption is below the expected pace. An **"On pace"** badge appears in green when usage is meaningfully under pace. Can be toggled off under **Settings → Layout → Pace-Aware Quota Colours** (enabled by default).
- **Usage rate badge**: Provider cards can now show a live **req/hr** burn-rate badge derived from history without any extra API calls. Toggle it on under **Settings → Layout → Show Usage Rate (req/hr)** (off by default). The badge is hidden when fewer than 30 minutes of history exist or after a quota reset.

### Fixed
- **HTTP 429 rate-limit cards now show orange instead of red**: A rate-limited provider is a temporary state, not a configuration error. Cards now render with a Warning (orange) tone so you know to wait rather than investigate.
- **Monitor offline status now shows relative time**: The status bar shows `"Monitor offline — last sync 7m ago"` instead of a generic "Connection lost" message or an absolute timestamp.
- **Stale-detection failures on non-UTC machines**: `fetched_at` timestamps in `provider_history` and `raw_snapshots` are now stored as Unix epoch integers instead of ISO-8601 text strings, eliminating `DateTimeKind.Unspecified` comparison failures that caused the stale-data indicator to fire incorrectly on machines not set to UTC.

### Security
- **SQL injection hardening**: Table names and ORDER BY clauses in `WebDatabaseRawTableReader` are now validated against an allowlist; LIMIT parameters are bounds-checked (1–10,000). An architecture guardrail test (`ProductionCode_DoesNotUseUnguardedSqlInterpolation`) enforces this going forward.
- **Added CodeQL, Semgrep, and Trivy security scanning**: Three complementary scanners now run on a sensible schedule — Semgrep and Trivy secret-scanning gate every PR (fast, pattern-based), while CodeQL deep semantic analysis and Trivy full vulnerability scanning run weekly. Results are uploaded to GitHub Security as SARIF.

### CI/CD
- Screenshot baseline comparison now runs on `windows-2025` for both generation and comparison, eliminating false failures caused by WPF font rendering differences between Windows 10 and Windows Server 2025.

## [2.3.1] - 2026-03-19

### Fixed
- **OpenAI Codex data no longer updating**: Codex usage stopped refreshing on 2026-03-14 due to a change in the OpenAI API response format. The parser now handles the new response shape correctly.
- **Stale data shown silently after re-authentication**: When a provider's auth token expires or is missing, the app now stores a visible "re-authenticate" message instead of continuing to show old cached data.
- **Stale data indicator**: Provider cards that have not refreshed in over an hour now show a "last refreshed X ago — data may be outdated" notice.
- **Circuit-breaker providers hidden from UI**: When a provider is temporarily paused due to repeated failures, the UI now shows a "Temporarily paused — next check at HH:MM" message with the pause duration and last error.
- **Config and startup errors now visible in health endpoint**: Failures during config loading or startup are now reported in the monitor health endpoint instead of being silently swallowed.
- **Connectivity check returned misleading 404**: The provider connectivity check endpoint now returns 503 with a clear message when no usage data is available.

### Changed
- **`provider_history` write deduplication**: The database no longer stores rows when nothing has changed. Only `fetched_at` is updated on the existing row so the stale-data detector stays accurate. During idle periods this eliminates virtually all writes.
- **Automatic `provider_history` compaction**: Once per day, old history rows are downsampled — rows 7–90 days old to one per hour, rows older than 90 days to one per day. A `VACUUM` follows to reclaim freed disk space.
- **Monitor version logged on startup**: The monitor now logs its full version (including pre-release suffix) on every startup, making it easy to confirm from log files which build is running.

## [2.3.1-beta.4] - 2026-03-19

### Added
- **Integration tests for database read paths and pipeline**: Added 28 real-SQLite integration tests covering `GetHistoryAsync`, `GetHistoryByProviderAsync`, `GetRecentHistoryAsync`, and `GetLatestHistoryAsync` — verifying Dapper type mapping, stale-data detection, the full provider-data-to-database pipeline, and circuit-breaker deduplication behaviour. These tests would have caught the beta.3 `Int64`/`Int32` crash before release.

## [2.3.1-beta.3] - 2026-03-19

### Changed
- **`provider_history` write deduplication**: The database no longer stores rows when nothing has changed. Before each write, the last stored row for each provider is checked against the incoming data. If the quota numbers, availability, status message, reset time, and sub-quota details are all identical, no new row is inserted — instead, only `fetched_at` is updated on the existing row so the stale-data detector stays accurate. During idle periods this eliminates virtually all writes; only genuine state changes are recorded.
- **Automatic `provider_history` compaction**: Once per day, old history rows are downsampled: rows 7–90 days old are reduced to one per hour per provider; rows older than 90 days are reduced to one per day. A `VACUUM` runs afterwards to reclaim freed disk space. This acts as a safety valve for intensive-use periods where data changes on every poll.

## [2.3.1-beta.2] - 2026-03-18

### Fixed
- **OpenAI Codex data no longer updating**: Codex usage stopped refreshing on 2026-03-14 due to a change in the OpenAI API response format. The parser now handles the new response shape correctly. If your Codex card has been showing the same data for days, it will update automatically on the next refresh cycle.
- **Stale data shown silently after re-authentication**: When a provider's auth token expires or is missing, the app now stores a visible "re-authenticate" message instead of continuing to show old cached data. After a database wipe or first run, you will see an actionable message rather than an empty card.
- **Stale data indicator**: Provider cards that have not refreshed in over an hour now show a "last refreshed X ago — data may be outdated" notice, so you always know when the data is fresh versus cached.
- **Circuit-breaker providers hidden from UI**: When a provider is temporarily paused due to repeated failures, the UI now shows a "Temporarily paused — next check at HH:MM" message instead of silently serving stale cached data or showing nothing. The pause duration and last error are included so you know when it will retry.
- **Config and startup errors now visible in health endpoint**: Failures during config loading or startup (e.g. a corrupted config file) are now reported in the monitor health endpoint instead of being silently swallowed.
- **Connectivity check returned misleading 404**: The provider connectivity check endpoint now returns 503 with a clear message when no usage data is available, rather than a 404 that implied the endpoint itself was missing.

## [2.3.1-beta.1] - 2026-03-18

### Removed
- **AnthropicProvider**: Removed the non-functional stub provider that returned hardcoded responses with no real API integration.

### Changed
- **ProviderBase Helpers**: Added `CreateBearerRequest()` and `DeserializeJsonOrDefault<T>()` helpers to `ProviderBase`, eliminating repeated boilerplate across all provider implementations.

### CI/CD
- Updated all GitHub Actions to latest major versions (checkout v6, setup-dotnet v5, upload-artifact v7, download-artifact v8, github-script v8, cache v5, codecov v5, create-pull-request v8, paths-filter v4) to eliminate Node.js 20 deprecation warnings.
