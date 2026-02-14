# Database Planning Document

## Current Database Status

### Implementation Overview
The AI Consumption Tracker agent stores usage data in an embedded SQLite database (`agent.db`) with automatic refresh intervals.

### Current Configuration
- **Database Type:** SQLite (libsql)
- **Refresh Interval:** User-configurable (default: 5 minutes = 300 seconds)
- **Storage Location:** `./agent.db` (relative to executable)
- **Auto-refresh:** Enabled by default

### Data Storage Strategy
Currently, the agent stores **every reading** from every provider on every refresh cycle where:
- Provider is available (`is_available = true`)
- Usage is non-zero (`cost_used > 0`)

### Schema
```sql
CREATE TABLE usage_records (
    id TEXT PRIMARY KEY,           -- UUID v4 (36 chars)
    provider_id TEXT NOT NULL,     -- Provider identifier
    provider_name TEXT NOT NULL,   -- Display name
    usage REAL NOT NULL,           -- Cost/usage value
    "limit" REAL,                  -- Cost limit (optional)
    usage_unit TEXT NOT NULL,      -- Unit (Tokens, USD, etc.)
    is_quota_based INTEGER NOT NULL, -- Boolean flag
    timestamp TEXT NOT NULL,       -- ISO 8601 format
    next_reset_time TEXT           -- Optional reset time
);

CREATE INDEX idx_provider_id ON usage_records(provider_id);
CREATE INDEX idx_timestamp ON usage_records(timestamp);
CREATE INDEX idx_provider_timestamp ON usage_records(provider_id, timestamp);
```

---

## Database Size Projection (1 Year)

### Current Provider Count
**17 active providers:**
1. OpenAI
2. Anthropic (Claude Code)
3. DeepSeek
4. Simulated
5. OpenRouter
6. OpenCode Zen
7. Codex
8. GitHub Copilot
9. Antigravity
10. Kimi
11. MiniMax (China)
12. MiniMax (International)
13. Z.AI
14. Synthetic
15. Generic Pay-As-You-Go
16. Gemini
17. Mistral (when implemented)

### Storage Math

#### Records Calculation
```
Refresh interval:    5 minutes
Records per hour:    12 (60/5)
Records per day:     288 (12 × 24)
Records per year:    105,120 (288 × 365)
Records all providers (17): 1,787,040 per year
```

#### Record Size Breakdown
| Field | Type | Approx. Size |
|-------|------|--------------|
| id | TEXT (UUID) | 36 bytes |
| provider_id | TEXT | 20 bytes |
| provider_name | TEXT | 30 bytes |
| usage | REAL | 8 bytes |
| limit | REAL (NULLable) | 9 bytes |
| usage_unit | TEXT | 10 bytes |
| is_quota_based | INTEGER | 1 byte |
| timestamp | TEXT | 25 bytes |
| next_reset_time | TEXT (NULLable) | 26 bytes |
| **Row overhead** | | 8 bytes |
| **Total per record** | | **~173 bytes** |

#### Projected Database Size
```
Raw data:           1,787,040 records × 173 bytes = 309 MB
SQLite overhead:    ~30% (indexes, WAL, metadata) = +93 MB
Index storage:      ~20% = +62 MB
─────────────────────────────────────────────
Estimated total:    ~464 MB per year
```

**With additional tables (reset_events, etc.):**
- Estimated total: **~500-600 MB per year**

---

## Problem Statement

### Issues with Current Approach

1. **Excessive Storage Growth**
   - 500+ MB per year is too large for a simple usage tracker
   - Most records contain identical data (no usage change)

2. **No Data Lifecycle Management**
   - Records accumulate indefinitely
   - No automatic cleanup of old data

3. **Inefficient Query Performance**
   - Large tables slow down historical queries
   - Indexes become bloated over time

4. **Backup/Transfer Issues**
   - Large database files are harder to backup
   - Slower sync if cloud backup is implemented

---

## Proposed Optimization Strategy

### Phase 1: Delta-Only Storage (Immediate)

**Rule:** Only store a record if the usage value differs from the last stored record.

**Implementation:**
```rust
// Pseudocode
if usage_changed(provider_id, new_usage) || heartbeat_due(provider_id) {
    store_record();
}
```

**Expected Impact:**
- Reduces records by **60-80%** (most providers don't change every 5 minutes)
- Estimated annual size: **100-150 MB**

### Phase 2: Tiered Retention Policy

**Sampling Strategy:**

| Time Period | Sampling Rate | Retention |
|-------------|---------------|-------------|
| Last 24 hours | Every change | All records |
| 24h - 7 days | Every 15 min | Hourly snapshots |
| 7 - 30 days | Every hour | 4x daily snapshots |
| 30 - 90 days | Every 4 hours | Daily snapshots |
| > 90 days | N/A | Auto-delete |

**Implementation:**
- Background cleanup job runs daily
- Aggregates old data into summary records
- Deletes individual records after aggregation

**Expected Impact:**
- Reduces records by **90%+** after 90 days
- Estimated annual size: **30-50 MB**

### Phase 3: Alternative Database Options

#### Option A: Keep SQLite (Optimized)
**Pros:**
- Battle-tested, widely supported
- Excellent tooling (DB Browser for SQLite)
- Zero configuration

**Cons:**
- Single-writer limitation
- WAL mode required for concurrency

**Optimization:**
- Enable WAL mode: `PRAGMA journal_mode=WAL;`
- Set cache size: `PRAGMA cache_size = -64000;` (64MB)
- Use `VACUUM` periodically to reclaim space

#### Option B: Redb (Rust-native)
**Pros:**
- Pure Rust, zero dependencies
- ACID compliant
- Better concurrency than SQLite
- Smaller binary size

**Cons:**
- Less mature ecosystem
- No GUI tools
- Breaking API changes possible

**Use case:** Good for new projects, but SQLite is proven.

#### Option C: RocksDB (Facebook)
**Pros:**
- Extremely fast writes (LSM-tree)
- Facebook/Instagram proven
- Compression support

**Cons:**
- Complex configuration
- Larger binary size
- C++ dependency

**Use case:** High-write workloads, but overkill for this use case.

**Recommendation:** Stick with SQLite but optimize usage patterns.

---

## Implementation Plan

### Priority 1: Delta-Only Storage (Week 1)
- [ ] Modify `refresh_and_store` to check last record
- [ ] Add heartbeat (store at least every hour even if no change)
- [ ] Test with all providers

### Priority 2: Tiered Retention (Week 2-3)
- [ ] Implement background cleanup job
- [ ] Create aggregation logic
- [ ] Add retention configuration to settings
- [ ] Add manual "Clear History" button

### Priority 3: Monitoring (Week 4)
- [ ] Add database size to health check endpoint
- [ ] Log database size on startup
- [ ] Alert if database > 100MB

---

## Migration Path

### For Existing Users
1. **Automatic:** On next update, enable delta-only storage immediately
2. **Optional:** Provide "Compact Database" button to run VACUUM and cleanup
3. **Future:** Background migration to tiered retention (transparent to user)

### Database Version Management
- Add `db_version` table
- Future migrations can be automated
- Allow rollback if issues occur

---

## Success Metrics

| Metric | Current | Target (3 months) |
|--------|---------|-------------------|
| DB size (1 year projection) | 500 MB | < 50 MB |
| Records per day (17 providers) | 4,896 | < 500 |
| Query time (30-day history) | < 100ms | < 20ms |
| Startup time | < 500ms | < 200ms |

---

## Technical Notes

### Why Not Store Everything?
- **Use case:** Users want to see trends, not every 5-minute snapshot
- **Value diminishes:** 6-month-old 5-minute data has low utility
- **Storage cost:** Mobile/cloud sync becomes expensive

### Edge Cases to Handle
1. **Clock changes:** DST adjustments shouldn't trigger false deltas
2. **Provider errors:** Don't store error states as usage records
3. **Privacy mode:** Mask/hide sensitive data in historical records
4. **Export/Import:** Maintain compatibility for backup/restore

---

## Related Files

- `aic_agent/src/database.rs` - Database operations
- `aic_agent/src/main.rs` - `refresh_and_store` function
- `aic_agent/src/scheduler.rs` - Background refresh logic
- `DESIGN.md` - Architecture overview

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2025-02-14 | Created planning doc | Database growing too large |
| TBD | Implement delta-only | High impact, low effort |
| TBD | Keep SQLite | Proven, optimized approach |
| TBD | Add tiered retention | Balance detail vs. storage |

---

*Last updated: 2025-02-14*
*Next review: After Phase 1 implementation*
