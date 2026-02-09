//! Database Schema and Migrations for AI Consumption Tracker
//!
//! # Schema Overview
//!
//! ## Tables
//!
//! ### Schema Version Management
//! - `schema_migrations` - Tracks applied migrations
//!
//! ### Core Tables
//! - `usage_records` - Individual usage snapshots from providers
//! - `provider_metadata` - Aggregated provider statistics
//!
//! ### Configuration Tables
//! - `provider_configs` - Stored provider configurations
//! - `daily_summaries` - Daily aggregated usage data
//!
//! ### Analytics Tables
//! - `usage_trends` - Trend analysis data
//!
//! # Migration Strategy
//!
//! Migrations are applied sequentially with version tracking.
//! Each migration is idempotent and can be safely re-run.

use aic_core::{PaymentType, ProviderConfig, ProviderUsage, ProviderUsageDetail};
use chrono::{DateTime, Duration, Utc};
use libsql::{Builder, Connection, Value};
use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use thiserror::Error;
use uuid::Uuid;

#[derive(Debug, Error)]
pub enum DatabaseError {
    #[error("Connection error: {0}")]
    ConnectionError(String),
    #[error("Migration error: {0}")]
    MigrationError(String),
    #[error("Serialization error: {0}")]
    SerializationError(String),
    #[error("Invalid config: {0}")]
    InvalidConfig(String),
    #[error("Query error: {0}")]
    QueryError(String),
    #[error("Migration {version} already applied")]
    MigrationAlreadyApplied { version: i32 },
    #[error("Rollback error: {0}")]
    RollbackError(String),
}

impl std::convert::From<libsql::Error> for DatabaseError {
    fn from(err: libsql::Error) -> Self {
        DatabaseError::QueryError(err.to_string())
    }
}

pub type Result<T> = std::result::Result<T, DatabaseError>;

#[derive(Clone)]
pub struct DatabaseConfig {
    pub path: PathBuf,
    pub retention_days: u32,
    pub turso_url: Option<String>,
    pub turso_auth_token: Option<String>,
}

impl Default for DatabaseConfig {
    fn default() -> Self {
        let base_dir = directories::BaseDirs::new()
            .map(|base| base.home_dir().join(".ai-consumption-tracker"))
            .unwrap_or_else(|| PathBuf::from(".ai-consumption-tracker"));

        Self {
            path: base_dir.join("usage.db"),
            retention_days: 30,
            turso_url: None,
            turso_auth_token: None,
        }
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UsageRecord {
    pub id: String,
    pub provider_id: String,
    pub provider_name: String,
    pub usage_percentage: f64,
    pub cost_used: f64,
    pub cost_limit: f64,
    pub payment_type: String,
    pub usage_unit: String,
    pub is_quota_based: bool,
    pub is_available: bool,
    pub description: String,
    pub auth_source: String,
    pub details: Option<String>,
    pub account_name: String,
    pub next_reset_time: Option<DateTime<Utc>>,
    pub recorded_at: DateTime<Utc>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ProviderMetadata {
    pub provider_id: String,
    pub provider_name: String,
    pub first_seen_at: DateTime<Utc>,
    pub last_seen_at: DateTime<Utc>,
    pub total_records: i64,
    pub last_usage_percentage: f64,
    pub last_is_available: bool,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ProviderConfigRecord {
    pub id: String,
    pub provider_id: String,
    pub api_key_encrypted: Option<String>,
    pub config_type: String,
    pub base_url: Option<String>,
    pub show_in_tray: bool,
    pub enabled_sub_trays: String,
    pub limit: f64,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct DailySummary {
    pub id: String,
    pub provider_id: String,
    pub date: String,
    pub avg_usage_percentage: f64,
    pub total_cost_used: f64,
    pub total_cost_limit: f64,
    pub availability_percentage: f64,
    pub record_count: i64,
    pub created_at: DateTime<Utc>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UsageTrend {
    pub id: String,
    pub provider_id: String,
    pub period_start: DateTime<Utc>,
    pub period_end: DateTime<Utc>,
    pub trend_direction: String,
    pub avg_percentage_change: f64,
    pub prediction: Option<String>,
    pub confidence: Option<f64>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct AggregatedUsage {
    pub provider_id: String,
    pub period_start: DateTime<Utc>,
    pub period_end: DateTime<Utc>,
    pub avg_usage_percentage: Option<f64>,
    pub total_cost_used: Option<f64>,
    pub record_count: i64,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct DatabaseStatistics {
    pub total_records: i64,
    pub total_providers: i64,
    pub total_configs: i64,
    pub oldest_record: Option<DateTime<Utc>>,
    pub newest_record: Option<DateTime<Utc>>,
    pub database_size_bytes: u64,
    pub schema_version: i32,
}

#[derive(Clone, Debug)]
pub struct Migration {
    pub version: i32,
    pub description: &'static str,
    pub sql: &'static str,
}

const CURRENT_SCHEMA_VERSION: i32 = 1;

const MIGRATIONS: &[Migration] = &[
    Migration {
        version: 1,
        description: "Initial schema with usage_records and provider_metadata",
        sql: r#"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                description TEXT
            );

            CREATE TABLE IF NOT EXISTS usage_records (
                id TEXT PRIMARY KEY,
                provider_id TEXT NOT NULL,
                provider_name TEXT NOT NULL,
                usage_percentage REAL NOT NULL,
                cost_used REAL NOT NULL,
                cost_limit REAL NOT NULL,
                payment_type TEXT NOT NULL,
                usage_unit TEXT NOT NULL,
                is_quota_based INTEGER NOT NULL DEFAULT 0,
                is_available INTEGER NOT NULL DEFAULT 0,
                description TEXT NOT NULL,
                auth_source TEXT NOT NULL,
                details TEXT,
                account_name TEXT NOT NULL,
                next_reset_time TEXT,
                recorded_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS provider_metadata (
                provider_id TEXT PRIMARY KEY,
                provider_name TEXT NOT NULL,
                first_seen_at TEXT NOT NULL,
                last_seen_at TEXT NOT NULL,
                total_records INTEGER NOT NULL DEFAULT 0,
                last_usage_percentage REAL NOT NULL,
                last_is_available INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_usage_provider_id ON usage_records(provider_id);
            CREATE INDEX IF NOT EXISTS idx_usage_recorded_at ON usage_records(recorded_at);
            CREATE INDEX IF NOT EXISTS idx_usage_provider_time ON usage_records(provider_id, recorded_at);
            CREATE INDEX IF NOT EXISTS idx_metadata_provider ON provider_metadata(provider_id);
        "#,
    },
    Migration {
        version: 2,
        description: "Add provider configurations and daily summaries",
        sql: r#"
            CREATE TABLE IF NOT EXISTS provider_configs (
                id TEXT PRIMARY KEY,
                provider_id TEXT NOT NULL,
                api_key_encrypted TEXT,
                config_type TEXT NOT NULL DEFAULT 'api',
                base_url TEXT,
                show_in_tray INTEGER NOT NULL DEFAULT 0,
                enabled_sub_trays TEXT DEFAULT '[]',
                "limit" REAL DEFAULT 100.0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                UNIQUE(provider_id)
            );

            CREATE TABLE IF NOT EXISTS daily_summaries (
                id TEXT PRIMARY KEY,
                provider_id TEXT NOT NULL,
                date TEXT NOT NULL,
                avg_usage_percentage REAL NOT NULL,
                total_cost_used REAL NOT NULL,
                total_cost_limit REAL NOT NULL,
                availability_percentage REAL NOT NULL,
                record_count INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                UNIQUE(provider_id, date)
            );

            CREATE INDEX IF NOT EXISTS idx_daily_provider_date ON daily_summaries(provider_id, date);
            CREATE INDEX IF NOT EXISTS idx_configs_provider ON provider_configs(provider_id);
        "#,
    },
    Migration {
        version: 3,
        description: "Add usage trends table for analytics",
        sql: r#"
            CREATE TABLE IF NOT EXISTS usage_trends (
                id TEXT PRIMARY KEY,
                provider_id TEXT NOT NULL,
                period_start TEXT NOT NULL,
                period_end TEXT NOT NULL,
                trend_direction TEXT NOT NULL,
                avg_percentage_change REAL NOT NULL,
                prediction TEXT,
                confidence REAL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_trends_provider ON usage_trends(provider_id);
            CREATE INDEX IF NOT EXISTS idx_trends_period ON usage_trends(period_start, period_end);
        "#,
    },
];

pub struct UsageDatabase {
    conn: Connection,
    config: DatabaseConfig,
}

impl UsageDatabase {
    pub async fn new(config: DatabaseConfig) -> Result<Self> {
        let conn = if let (Some(url), Some(token)) = (&config.turso_url, &config.turso_auth_token) {
            let builder = Builder::new_remote(url.clone(), token.clone());
            let db = builder.build().await.map_err(|e| DatabaseError::ConnectionError(e.to_string()))?;
            db.connect().map_err(|e| DatabaseError::ConnectionError(e.to_string()))?
        } else {
            let path = config.path.to_str().unwrap_or("usage.db");
            let builder = Builder::new_local(path);
            let db = builder.build().await.map_err(|e| DatabaseError::ConnectionError(e.to_string()))?;
            db.connect().map_err(|e| DatabaseError::ConnectionError(e.to_string()))?
        };

        let db = Self { conn, config };
        db.run_migrations().await?;
        Ok(db)
    }

    async fn run_migrations(&self) -> Result<()> {
        let applied_version = self.get_applied_version().await?;

        for migration in MIGRATIONS.iter() {
            if migration.version <= applied_version {
                continue;
            }

            let tx = self.conn.transaction().await.map_err(|e| {
                DatabaseError::MigrationError(e.to_string())
            })?;

            for statement in migration.sql.split(';').filter(|s| !s.trim().is_empty()) {
                tx.execute(statement.trim(), ()).await.map_err(|e| {
                    DatabaseError::MigrationError(format!("Migration {} failed: {}", migration.version, e))
                })?;
            }

            tx.execute(
                "INSERT INTO schema_migrations (version, description) VALUES (?, ?)",
                (migration.version, migration.description),
            ).await.map_err(|e| {
                DatabaseError::MigrationError(e.to_string())
            })?;

            tx.commit().await.map_err(|e| {
                DatabaseError::MigrationError(e.to_string())
            })?;
        }

        Ok(())
    }

    async fn get_applied_version(&self) -> Result<i32> {
        let result = self.conn.query(
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations",
            (),
        ).await;

        match result {
            Ok(mut rows) => {
                if let Some(row) = rows.next().await? {
                    let version: i32 = row.get(0)?;
                    Ok(version)
                } else {
                    Ok(0)
                }
            }
            Err(_) => Ok(0),
        }
    }

    fn payment_type_to_string(payment_type: &PaymentType) -> String {
        match payment_type {
            PaymentType::UsageBased => "UsageBased".to_string(),
            PaymentType::Credits => "Credits".to_string(),
            PaymentType::Quota => "Quota".to_string(),
        }
    }

    fn serialize_details(details: &Option<Vec<ProviderUsageDetail>>) -> Result<Option<String>> {
        match details {
            Some(d) => Ok(Some(serde_json::to_string(d)
                .map_err(|e| DatabaseError::SerializationError(e.to_string()))?)),
            None => Ok(None),
        }
    }

    async fn init_database(&self) -> Result<()> {
        let create_records_table = r#"
            CREATE TABLE IF NOT EXISTS usage_records (
                id TEXT PRIMARY KEY,
                provider_id TEXT NOT NULL,
                provider_name TEXT NOT NULL,
                usage_percentage REAL NOT NULL,
                cost_used REAL NOT NULL,
                cost_limit REAL NOT NULL,
                payment_type TEXT NOT NULL,
                usage_unit TEXT NOT NULL,
                is_quota_based INTEGER NOT NULL DEFAULT 0,
                is_available INTEGER NOT NULL DEFAULT 0,
                description TEXT NOT NULL,
                auth_source TEXT NOT NULL,
                details TEXT,
                account_name TEXT NOT NULL,
                next_reset_time TEXT,
                recorded_at TEXT NOT NULL
            )
        "#;

        let create_metadata_table = r#"
            CREATE TABLE IF NOT EXISTS provider_metadata (
                provider_id TEXT PRIMARY KEY,
                provider_name TEXT NOT NULL,
                first_seen_at TEXT NOT NULL,
                last_seen_at TEXT NOT NULL,
                total_records INTEGER NOT NULL DEFAULT 0,
                last_usage_percentage REAL NOT NULL,
                last_is_available INTEGER NOT NULL DEFAULT 0
            )
        "#;

        let create_indexes = vec![
            "CREATE INDEX IF NOT EXISTS idx_usage_provider_id ON usage_records(provider_id)",
            "CREATE INDEX IF NOT EXISTS idx_usage_recorded_at ON usage_records(recorded_at)",
            "CREATE INDEX IF NOT EXISTS idx_usage_provider_time ON usage_records(provider_id, recorded_at)",
        ];

        self.conn.execute(create_records_table, ()).await?;
        self.conn.execute(create_metadata_table, ()).await?;

        for index_sql in create_indexes {
            self.conn.execute(index_sql, ()).await?;
        }

        Ok(())
    }

    pub async fn store_usage(&self, usage_records: Vec<ProviderUsage>) -> Result<()> {
        for usage in usage_records {
            let record_id = Uuid::new_v4().to_string();
            let now = Utc::now();
            let payment_type_str = Self::payment_type_to_string(&usage.payment_type);
            let details_str = Self::serialize_details(&usage.details)?;
            let next_reset_time_str = usage.next_reset_time.map(|t| t.to_rfc3339());
            let recorded_at_str = now.to_rfc3339();

            let insert_record = r#"
                INSERT INTO usage_records (
                    id, provider_id, provider_name, usage_percentage,
                    cost_used, cost_limit, payment_type, usage_unit,
                    is_quota_based, is_available, description, auth_source,
                    details, account_name, next_reset_time, recorded_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            "#;

            self.conn.execute(insert_record, (
                record_id.as_str(),
                usage.provider_id.as_str(),
                usage.provider_name.as_str(),
                usage.usage_percentage,
                usage.cost_used,
                usage.cost_limit,
                payment_type_str.as_str(),
                usage.usage_unit.as_str(),
                usage.is_quota_based as i32,
                usage.is_available as i32,
                usage.description.as_str(),
                usage.auth_source.as_str(),
                details_str.as_deref().unwrap_or(""),
                usage.account_name.as_str(),
                next_reset_time_str.as_deref().unwrap_or(""),
                recorded_at_str.as_str(),
            )).await?;

            let upsert_metadata = r#"
                INSERT INTO provider_metadata (
                    provider_id, provider_name, first_seen_at, last_seen_at,
                    total_records, last_usage_percentage, last_is_available
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(provider_id) DO UPDATE SET
                    last_seen_at = excluded.last_seen_at,
                    total_records = total_records + 1,
                    last_usage_percentage = excluded.last_usage_percentage,
                    last_is_available = excluded.last_is_available
            "#;

            self.conn.execute(upsert_metadata, (
                usage.provider_id.as_str(),
                usage.provider_name.as_str(),
                now.to_rfc3339().as_str(),
                now.to_rfc3339().as_str(),
                1i64,
                usage.usage_percentage,
                usage.is_available as i32,
            )).await?;
        }

        Ok(())
    }

    pub async fn store_provider_config(&self, config: &ProviderConfig) -> Result<()> {
        let id = Uuid::new_v4().to_string();
        let now = Utc::now();
        let enabled_sub_trays = serde_json::to_string(&config.enabled_sub_trays)
            .map_err(|e| DatabaseError::SerializationError(e.to_string()))?;

        let upsert_config = r#"
            INSERT INTO provider_configs (
                id, provider_id, api_key_encrypted, config_type,
                base_url, show_in_tray, enabled_sub_trays, "limit",
                created_at, updated_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(provider_id) DO UPDATE SET
                api_key_encrypted = COALESCE(excluded.api_key_encrypted, provider_configs.api_key_encrypted),
                config_type = excluded.config_type,
                base_url = COALESCE(excluded.base_url, provider_configs.base_url),
                show_in_tray = excluded.show_in_tray,
                enabled_sub_trays = excluded.enabled_sub_trays,
                "limit" = excluded."limit",
                updated_at = excluded.updated_at
        "#;

        self.conn.execute(upsert_config, (
            id.as_str(),
            config.provider_id.as_str(),
            config.api_key.as_str(),
            config.config_type.as_str(),
            config.base_url.as_deref().unwrap_or(""),
            config.show_in_tray as i32,
            enabled_sub_trays.as_str(),
            config.limit.unwrap_or(100.0),
            now.to_rfc3339().as_str(),
            now.to_rfc3339().as_str(),
        )).await?;

        Ok(())
    }

    fn parse_datetime(s: &str) -> Option<DateTime<Utc>> {
        DateTime::parse_from_rfc3339(s).ok().map(|dt| dt.with_timezone(&Utc))
    }

    fn parse_optional_datetime(s: Option<&str>) -> Option<DateTime<Utc>> {
        s.and_then(|x| Self::parse_datetime(x))
    }

    async fn row_to_usage_record(row: &libsql::Row) -> Result<UsageRecord> {
        let recorded_at: String = row.get(15)?;
        let next_reset_time: Option<String> = row.get(14)?;

        Ok(UsageRecord {
            id: row.get(0)?,
            provider_id: row.get(1)?,
            provider_name: row.get(2)?,
            usage_percentage: row.get(3)?,
            cost_used: row.get(4)?,
            cost_limit: row.get(5)?,
            payment_type: row.get(6)?,
            usage_unit: row.get(7)?,
            is_quota_based: row.get::<i32>(8)? != 0,
            is_available: row.get::<i32>(9)? != 0,
            description: row.get(10)?,
            auth_source: row.get(11)?,
            details: row.get(12)?,
            account_name: row.get(13)?,
            next_reset_time: Self::parse_optional_datetime(next_reset_time.as_deref()),
            recorded_at: Self::parse_datetime(&recorded_at).unwrap_or_else(Utc::now),
        })
    }

    pub async fn get_usage_history(
        &self,
        provider_id: Option<&str>,
        start_time: Option<DateTime<Utc>>,
        end_time: Option<DateTime<Utc>>,
        limit: Option<i64>,
    ) -> Result<Vec<UsageRecord>> {
        let mut query = "SELECT * FROM usage_records WHERE 1=1".to_string();
        let mut params: Vec<Value> = Vec::new();

        if let Some(pid) = provider_id {
            query.push_str(" AND provider_id = ?");
            params.push(pid.to_string().into());
        }

        if let Some(start) = start_time {
            query.push_str(" AND recorded_at >= ?");
            params.push(start.to_rfc3339().into());
        }

        if let Some(end) = end_time {
            query.push_str(" AND recorded_at <= ?");
            params.push(end.to_rfc3339().into());
        }

        query.push_str(" ORDER BY recorded_at DESC");

        if let Some(l) = limit {
            query.push_str(&format!(" LIMIT {}", l));
        }

        let mut rows = self.conn.query(&query, params).await?;

        let mut records = Vec::new();
        while let Some(row) = rows.next().await? {
            records.push(Self::row_to_usage_record(&row).await?);
        }

        Ok(records)
    }

    async fn row_to_metadata(row: &libsql::Row) -> Result<ProviderMetadata> {
        let first_seen: String = row.get(2)?;
        let last_seen: String = row.get(3)?;

        Ok(ProviderMetadata {
            provider_id: row.get(0)?,
            provider_name: row.get(1)?,
            first_seen_at: Self::parse_datetime(&first_seen).unwrap_or_else(Utc::now),
            last_seen_at: Self::parse_datetime(&last_seen).unwrap_or_else(Utc::now),
            total_records: row.get(4)?,
            last_usage_percentage: row.get(5)?,
            last_is_available: row.get::<i32>(6)? != 0,
        })
    }

    pub async fn get_provider_metadata(&self, provider_id: &str) -> Result<Option<ProviderMetadata>> {
        let query = "SELECT * FROM provider_metadata WHERE provider_id = ?";

        let mut rows = self.conn.query(query, [provider_id.to_string()]).await?;

        if let Some(row) = rows.next().await? {
            Ok(Some(Self::row_to_metadata(&row).await?))
        } else {
            Ok(None)
        }
    }

    pub async fn get_latest_usage(&self) -> Result<Vec<UsageRecord>> {
        let query = r#"
            SELECT * FROM usage_records
            WHERE (provider_id, recorded_at) IN (
                SELECT provider_id, MAX(recorded_at)
                FROM usage_records
                GROUP BY provider_id
            )
            ORDER BY provider_id
        "#;

        let mut rows = self.conn.query(query, ()).await?;

        let mut records = Vec::new();
        while let Some(row) = rows.next().await? {
            records.push(Self::row_to_usage_record(&row).await?);
        }

        Ok(records)
    }

    pub async fn get_aggregated_usage(
        &self,
        provider_id: Option<&str>,
        start_time: DateTime<Utc>,
        end_time: DateTime<Utc>,
        period: &str,
    ) -> Result<Vec<AggregatedUsage>> {
        let time_format = match period {
            "hour" => "%Y-%m-%d %H:00:00",
            "day" => "%Y-%m-%d 00:00:00",
            "week" => "%Y-%W",
            "month" => "%Y-%m-01 00:00:00",
            _ => return Err(DatabaseError::InvalidConfig(format!("Invalid period: {}", period))),
        };

        let mut query = format!(
            r#"
                SELECT
                    provider_id,
                    datetime(MIN(recorded_at)) as period_start,
                    datetime(MAX(recorded_at)) as period_end,
                    AVG(usage_percentage) as avg_usage_percentage,
                    SUM(cost_used) as total_cost_used,
                    COUNT(*) as record_count
                FROM usage_records
                WHERE recorded_at BETWEEN ? AND ?
            "#
        );

        let mut params: Vec<Value> = vec![start_time.to_rfc3339().into(), end_time.to_rfc3339().into()];

        if let Some(pid) = provider_id {
            query.push_str(" AND provider_id = ?");
            params.push(pid.to_string().into());
        }

        query.push_str(&format!(
            " GROUP BY provider_id, strftime('{}', recorded_at) ORDER BY period_start",
            time_format
        ));

        let mut rows = self.conn.query(&query, params).await?;

        let mut aggregated = Vec::new();
        while let Some(row) = rows.next().await? {
            let period_start: String = row.get(1)?;
            let period_end: String = row.get(2)?;

            aggregated.push(AggregatedUsage {
                provider_id: row.get(0)?,
                period_start: Self::parse_datetime(&period_start).unwrap_or(start_time),
                period_end: Self::parse_datetime(&period_end).unwrap_or(end_time),
                avg_usage_percentage: row.get(3)?,
                total_cost_used: row.get(4)?,
                record_count: row.get(5)?,
            });
        }

        Ok(aggregated)
    }

    pub async fn delete_provider_data(&self, provider_id: &str) -> Result<u64> {
        let delete_records = "DELETE FROM usage_records WHERE provider_id = ?";
        let records_deleted = self.conn.execute(delete_records, [provider_id.to_string()]).await?;

        let delete_metadata = "DELETE FROM provider_metadata WHERE provider_id = ?";
        self.conn.execute(delete_metadata, [provider_id.to_string()]).await?;

        Ok(records_deleted)
    }

    pub async fn cleanup_old_records(&self) -> Result<u64> {
        let cutoff_time = Utc::now() - Duration::days(self.config.retention_days as i64);

        let query = "DELETE FROM usage_records WHERE recorded_at < ?";
        let rows_deleted = self.conn.execute(query, [cutoff_time.to_rfc3339()]).await?;

        let update_metadata = r#"
            UPDATE provider_metadata
            SET total_records = (
                SELECT COUNT(*) FROM usage_records
                WHERE usage_records.provider_id = provider_metadata.provider_id
            )
            WHERE provider_id IN (
                SELECT DISTINCT provider_id FROM usage_records
            )
        "#;

        self.conn.execute(update_metadata, ()).await?;

        Ok(rows_deleted)
    }

    pub async fn get_statistics(&self) -> Result<DatabaseStatistics> {
        let total_records_query = "SELECT COUNT(*) as count FROM usage_records";
        let mut total_records_row = self.conn.query(total_records_query, ()).await?;
        let total_records: i64 = if let Some(row) = total_records_row.next().await? {
            row.get(0)?
        } else {
            0
        };

        let total_providers_query = "SELECT COUNT(*) as count FROM provider_metadata";
        let mut total_providers_row = self.conn.query(total_providers_query, ()).await?;
        let total_providers: i64 = if let Some(row) = total_providers_row.next().await? {
            row.get(0)?
        } else {
            0
        };

        let total_configs_query = "SELECT COUNT(*) as count FROM provider_configs";
        let mut total_configs_row = self.conn.query(total_configs_query, ()).await?;
        let total_configs: i64 = if let Some(row) = total_configs_row.next().await? {
            row.get(0)?
        } else {
            0
        };

        let oldest_query = "SELECT MIN(recorded_at) as oldest FROM usage_records";
        let mut oldest_row = self.conn.query(oldest_query, ()).await?;
        let oldest_record = if let Some(row) = oldest_row.next().await? {
            let oldest_str: Option<String> = row.get(0)?;
            oldest_str.as_deref().and_then(Self::parse_datetime)
        } else {
            None
        };

        let newest_query = "SELECT MAX(recorded_at) as newest FROM usage_records";
        let mut newest_row = self.conn.query(newest_query, ()).await?;
        let newest_record = if let Some(row) = newest_row.next().await? {
            let newest_str: Option<String> = row.get(0)?;
            newest_str.as_deref().and_then(Self::parse_datetime)
        } else {
            None
        };

        let database_size_bytes = self.config.path.metadata().ok().map(|m| m.len()).unwrap_or(0);

        Ok(DatabaseStatistics {
            total_records,
            total_providers,
            total_configs,
            oldest_record,
            newest_record,
            database_size_bytes,
            schema_version: CURRENT_SCHEMA_VERSION,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    pub struct TestHarness {
        pub temp_dir: tempfile::TempDir,
        pub db: UsageDatabase,
    }

    impl TestHarness {
        pub async fn new() -> Self {
            let temp_dir = tempfile::TempDir::new().unwrap();
            let config = DatabaseConfig {
                path: temp_dir.path().join("test.db"),
                retention_days: 1,
                ..Default::default()
            };
            let db = UsageDatabase::new(config).await.unwrap();
            Self { temp_dir, db }
        }
    }

    #[tokio::test]
    async fn test_database_initialization() {
        let harness = TestHarness::new().await;
        assert!(harness.temp_dir.path().exists());
    }

    #[tokio::test]
    async fn test_store_usage() {
        let harness = TestHarness::new().await;

        let usage = vec![ProviderUsage {
            provider_id: "test_provider".to_string(),
            provider_name: "Test Provider".to_string(),
            usage_percentage: 75.5,
            cost_used: 25.50,
            cost_limit: 100.0,
            payment_type: PaymentType::UsageBased,
            usage_unit: "tokens".to_string(),
            is_quota_based: true,
            is_available: true,
            description: "Test usage record".to_string(),
            auth_source: "config".to_string(),
            details: None,
            account_name: "test@example.com".to_string(),
            next_reset_time: None,
        }];

        harness.db.store_usage(usage).await.unwrap();

        let history = harness.db.get_usage_history(None, None, None, None).await.unwrap();
        assert_eq!(history.len(), 1);
        assert_eq!(history[0].provider_id, "test_provider");
    }

    #[tokio::test]
    async fn test_get_provider_metadata() {
        let harness = TestHarness::new().await;

        let usage = vec![ProviderUsage {
            provider_id: "test_provider".to_string(),
            provider_name: "Test Provider".to_string(),
            usage_percentage: 75.5,
            cost_used: 25.50,
            cost_limit: 100.0,
            payment_type: PaymentType::UsageBased,
            usage_unit: "tokens".to_string(),
            is_quota_based: true,
            is_available: true,
            description: "Test usage record".to_string(),
            auth_source: "config".to_string(),
            details: None,
            account_name: "test@example.com".to_string(),
            next_reset_time: None,
        }];

        harness.db.store_usage(usage).await.unwrap();

        let metadata = harness.db.get_provider_metadata("test_provider").await.unwrap();
        assert!(metadata.is_some());
        let meta = metadata.unwrap();
        assert_eq!(meta.provider_id, "test_provider");
        assert_eq!(meta.total_records, 1);
        assert_eq!(meta.last_is_available, true);
    }

    #[tokio::test]
    async fn test_cleanup_old_records() {
        let harness = TestHarness::new().await;

        let usage = vec![ProviderUsage {
            provider_id: "test_provider".to_string(),
            provider_name: "Test Provider".to_string(),
            usage_percentage: 75.5,
            cost_used: 25.50,
            cost_limit: 100.0,
            payment_type: PaymentType::UsageBased,
            usage_unit: "tokens".to_string(),
            is_quota_based: true,
            is_available: true,
            description: "Test usage record".to_string(),
            auth_source: "config".to_string(),
            details: None,
            account_name: "test@example.com".to_string(),
            next_reset_time: None,
        }];

        harness.db.store_usage(usage).await.unwrap();

        let records_before = harness.db.get_usage_history(None, None, None, None).await.unwrap();
        assert_eq!(records_before.len(), 1);

        let deleted = harness.db.cleanup_old_records().await.unwrap();
        assert_eq!(deleted, 0);

        let records_after = harness.db.get_usage_history(None, None, None, None).await.unwrap();
        assert_eq!(records_after.len(), 1);
    }

    #[tokio::test]
    async fn test_delete_provider_data() {
        let harness = TestHarness::new().await;

        let usage = vec![ProviderUsage {
            provider_id: "test_provider".to_string(),
            provider_name: "Test Provider".to_string(),
            usage_percentage: 75.5,
            cost_used: 25.50,
            cost_limit: 100.0,
            payment_type: PaymentType::UsageBased,
            usage_unit: "tokens".to_string(),
            is_quota_based: true,
            is_available: true,
            description: "Test usage record".to_string(),
            auth_source: "config".to_string(),
            details: None,
            account_name: "test@example.com".to_string(),
            next_reset_time: None,
        }];

        harness.db.store_usage(usage).await.unwrap();

        let deleted = harness.db.delete_provider_data("test_provider").await.unwrap();
        assert_eq!(deleted, 1);

        let metadata = harness.db.get_provider_metadata("test_provider").await.unwrap();
        assert!(metadata.is_none());
    }

    #[tokio::test]
    async fn test_get_statistics() {
        let harness = TestHarness::new().await;

        let usage = vec![ProviderUsage {
            provider_id: "test_provider".to_string(),
            provider_name: "Test Provider".to_string(),
            usage_percentage: 75.5,
            cost_used: 25.50,
            cost_limit: 100.0,
            payment_type: PaymentType::UsageBased,
            usage_unit: "tokens".to_string(),
            is_quota_based: true,
            is_available: true,
            description: "Test usage record".to_string(),
            auth_source: "config".to_string(),
            details: None,
            account_name: "test@example.com".to_string(),
            next_reset_time: None,
        }];

        harness.db.store_usage(usage).await.unwrap();

        let stats = harness.db.get_statistics().await.unwrap();
        assert_eq!(stats.total_records, 1);
        assert_eq!(stats.total_providers, 1);
        assert!(stats.oldest_record.is_some());
        assert!(stats.newest_record.is_some());
    }
}
