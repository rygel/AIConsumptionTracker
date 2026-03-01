# TODO

## Feature Backlog

- [x] Slim UI logging migration (Priority: P1, Effort: M): Replace ad-hoc `Debug.WriteLine`/`Console.WriteLine` diagnostics with `ILogger` (structured levels, timestamped output, centralized configuration).
- [x] Monitor logging format unification (Priority: P1, Effort: S/M): Migrate Monitor `[DIAG]`/custom diagnostic output to `ILogger` with timestamped, structured logs so Monitor and Slim UI diagnostics use one consistent format.
- [x] Burn-rate forecasting (Priority: P1, Effort: M): Estimate days until quota or budget exhaustion from recent usage trends.
- [x] Anomaly detection (experimental) (Priority: P1, Effort: M): Detect and flag unusual spikes or drops in provider usage.
- [x] Provider reliability panel (Priority: P2, Effort: M): Show provider latency, failure rate, and last successful sync timestamp.
- [ ] OpenCode CLI local provider (Priority: P2, Effort: M): Read detailed usage data from `opencode stats` CLI (sessions, messages, per-model breakdown, daily history) instead of just API credits endpoint
- [ ] Budget policies (Priority: P2, Effort: M): Add weekly/monthly provider budget caps with warning levels and optional soft-lock behavior.
- [ ] Comparison views (Priority: P3, Effort: S/M): Add period-over-period comparisons (day/week/month) and provider leaderboard by cost and growth.
- [ ] Data portability (Priority: P3, Effort: S): Support CSV/JSON export and import, plus scheduled encrypted SQLite backups.
- [ ] Plugin-style provider SDK (Priority: P3, Effort: L): Add a provider extension model with shared auth/HTTP/parsing helpers and conformance tests.
- [ ] Alert rules and notifications (Priority: P4, Effort: S/M): Add per-provider thresholds for remaining quota, spend percentage, and API failures with desktop and webhook notifications.

## Migration Plan: Provider-Owned Usage Details (Strict Contract)

- [x] Freeze strict detail schema in Core (Priority: P1, Effort: S): Keep `ProviderUsageDetail.DetailType`, add `WindowKind` (`Primary`, `Secondary`, `Spark`, `None`), and define required-field rules for every emitted detail row.
- [x] Make provider output the single source of truth (Priority: P1, Effort: M): Update all providers that emit `Details` to always set `DetailType`/`WindowKind`; treat missing or `Unknown` values as provider bugs.
- [x] Remove all runtime heuristics in clients (Priority: P1, Effort: M): Delete name-based fallback parsing from Slim UI, Web UI, and CLI; render/filter only from typed fields.
- [x] Remove fallback helpers from Core model (Priority: P1, Effort: S): Delete string-name classification helpers once providers and clients are fully typed to avoid dual logic paths.
- [x] Add strict runtime validation in Monitor (Priority: P1, Effort: M): Validate detail contract during refresh and return explicit provider error states when typed fields are invalid.
- [x] Add/extend tests for strict mode (Priority: P1, Effort: M): Add tests that fail when providers emit invalid detail typing and tests that assert typed rendering paths only.
- [x] Enforce in CI (Priority: P2, Effort: S): Add a guard test/job that fails if any provider output includes untyped/invalid detail rows.
- [x] Legacy history strategy without runtime fallback (Priority: P2, Effort: S/M): Keep old rows as historical data, but do not add runtime backfill heuristics; optional one-off migration script only if needed.
- [x] Document contract and implementation checklist (Priority: P2, Effort: S): Update docs with strict expectations, rollout steps, and provider implementation examples.

## Resilience Plan: Monitor Startup and Port Binding

- [x] Handle bind race at runtime (Priority: P1, Effort: M): Wrap startup bind in retry logic for `AddressInUseException`; on collision, select a new port and retry with bounded attempts and structured logs.
- [x] Move monitor metadata write after successful bind (Priority: P1, Effort: S): Do not write `monitor.json` before the monitor is actually listening; publish PID/port only after startup succeeds.
- [x] Add startup synchronization (Priority: P1, Effort: M): Add a machine-local startup lock (mutex or lock file) so concurrent launch attempts cannot race each other.
- [x] Add stale metadata guard (Priority: P1, Effort: S): On client side, if `monitor.json` PID is dead or health fails, invalidate stale metadata and force fresh discovery/start flow.
- [x] Add explicit startup failure reporting (Priority: P1, Effort: S): Write startup failure reason to monitor log and `monitor.json` error field when startup aborts.
- [x] Tighten launcher idempotency (Priority: P2, Effort: M): In `MonitorLauncher`, avoid duplicate starts by re-checking health/PID under lock before and after spawn.
- [x] Add integration test for port-collision recovery (Priority: P1, Effort: M): Simulate occupied preferred port and verify monitor recovers on alternate port without crash.
- [x] Add integration test for concurrent starts (Priority: P1, Effort: M): Trigger parallel start attempts and assert exactly one running monitor with valid `monitor.json`.
- [x] Add regression test for stale metadata recovery (Priority: P2, Effort: S): Seed dead PID/old port in `monitor.json` and verify client reconnects without persistent stale-state warnings.
