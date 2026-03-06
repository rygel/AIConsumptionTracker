# Changelog

## [Unreleased] - Beta 19 preparation

### Fixed
- **Kimi Dual Bars**: Fixed `DetermineWindowKind` not emitting `WindowKind.Primary` for daily limits, preventing dual progress bars from rendering.
- **Synthetic Reset Time**: Fixed `BuildResetLabel` double-converting UTC timestamps by adding `DateTimeStyles.RoundtripKind` during parse, resolving "resets in 0 minutes" display.
- **ProviderMetadataCatalog**: Fixed stale inline `ProviderMetadata` stubs for GitHub Copilot, Mistral, and Kimi left over from the Beta 18 Anthropic cleanup; now references each provider's own `StaticDefinition`.

### Removed
- **Anthropic Provider**: Fully removed `AnthropicProvider.cs`, its catalog entry, and `AnthropicProviderTests.cs`.

### Refactored
- Extracted shared JSON navigation helpers (`ReadString`, `ReadDouble`, `ReadBool`) into `JsonElementExtensions` in `AIUsageTracker.Core.Helpers`, eliminating identical private methods duplicated in `OpenAIProvider` and `CodexProvider`.

## [2.2.28-beta.18] - 2026-03-06

### Added
- **Dual Progress Bars**: Added full support for displaying primary and secondary quota progress bars simultaneously for Kimi, OpenAI Codex, and GitHub Copilot.
- **UI Refactoring**: Consolidate provider rendering to improve the layout footprint and fix the dual pass issue in the Slim UI.

### Removed
- **Providers**: Removed Anthropic and OpenAI providers from the codebase.

### Fixed
- **Antigravity Tracking**: Fixed an issue where the local Antigravity server probe would hang on incorrect listening ports by adding an explicit HTTP timeout.

## [2.2.28-beta.10] - 2026-03-05

### Added
- **Raw Snapshot Fields**: All providers now populate RawJson and HttpStatus fields for improved debugging and monitoring
  - Added tests for AnthropicProvider, GeminiProvider, OpenCodeZenProvider to verify field population
  - Added test fixtures for antigravity, github-copilot, and synthetic providers
- **Gemini Provider Improvements**: Enhanced GeminiProvider with path override support for testing and improved error handling

### Removed
- **EvolveMigrationProvider**: Deleted deprecated provider (191 lines)

### Fixed
- **Kimi Provider Dual Progress Bars**: Added `DetermineWindowKind()` method to correctly set `WindowKind.Secondary` for weekly limits (7+ days), enabling dual progress bar display in UI

### CI/CD
- Added Web Tests job to CI pipeline
- Improved Playwright browser installation in CI workflow
- Fixed path handling in GitHub Actions PowerShell scripts

## [2.2.26] - 2026-02-28

### Added
- Dual release channel support (Stable and Beta)
- Update channel selector in Settings window
- develop branch for beta releases

### Changes
- Solution file updated to reference AIUsageTracker.* projects
- App icon now properly embedded in all executables

### CI/CD
- New release.yml workflow with channel parameter
- publish.yml updated to detect beta releases from tag patterns
- generate-appcast.sh script with channel support
- Beta appcast XML files for all architectures

### Application
- UpdateChannel enum in Core (Stable/Beta)
- GitHubUpdateChecker now uses channel-specific appcast URLs

## [2.2.25] - 2026-02-27

### Added
- Pay-as-you-go support for Mistral AI
- Automatic retry for Gemini API on common failures

### Fixed
- Anthropic authentication error handling
- Layout issues on high-DPI displays
