use std::path::PathBuf;
use std::sync::Arc;
use std::time::Duration;

use aic_core::{ProviderUsage, ConfigLoader};
use aic_database::{DatabaseConfig, Result, UsageDatabase};
use chrono::{DateTime, Utc};
use log::info;
use serde::{Deserialize, Serialize};
use tokio::sync::Mutex;

pub mod api;
pub mod config;
mod polling;

pub use config::AgentConfig;
pub use polling::PollingService;

#[derive(Clone)]
pub struct AgentState {
    pub db: Arc<UsageDatabase>,
    pub config: Arc<Mutex<AgentConfig>>,
    pub last_refresh: Arc<Mutex<Option<DateTime<Utc>>>>,
    pub is_polling: Arc<Mutex<bool>>,
}

impl AgentState {
    pub fn new(
        db: Arc<UsageDatabase>,
        config: Arc<Mutex<AgentConfig>>,
    ) -> Self {
        Self {
            db,
            config,
            last_refresh: Arc::new(Mutex::new(None)),
            is_polling: Arc::new(Mutex::new(false)),
        }
    }
}

#[derive(Clone, Serialize, Deserialize)]
pub struct AgentStatus {
    pub running: bool,
    pub polling_enabled: bool,
    pub polling_interval_seconds: u64,
    pub last_refresh: Option<DateTime<Utc>>,
    pub next_refresh: Option<DateTime<Utc>>,
    pub database_path: PathBuf,
    pub configured_providers: Vec<String>,
    pub active_providers: Vec<String>,
}

#[derive(Clone, Serialize, Deserialize, Default)]
pub struct ApiResponse<T> {
    pub success: bool,
    pub data: Option<T>,
    pub error: Option<String>,
    pub timestamp: DateTime<Utc>,
}

impl<T> ApiResponse<T> {
    pub fn success(data: T) -> Self {
        Self {
            success: true,
            data: Some(data),
            error: None,
            timestamp: Utc::now(),
        }
    }

    pub fn error(message: String) -> Self {
        Self {
            success: false,
            data: None,
            error: Some(message),
            timestamp: Utc::now(),
        }
    }
}

#[derive(Clone, Serialize, Deserialize)]
pub struct UsageSummary {
    pub total_providers: usize,
    pub active_providers: usize,
    pub total_usage_records: i64,
    pub records_today: i64,
    pub providers: Vec<ProviderSummary>,
}

#[derive(Clone, Serialize, Deserialize)]
pub struct ProviderSummary {
    pub provider_id: String,
    pub provider_name: String,
    pub is_available: bool,
    pub usage_percentage: f64,
    pub cost_used: f64,
    pub cost_limit: f64,
    pub last_updated: Option<DateTime<Utc>>,
}

pub struct UsageAgent {
    state: AgentState,
}

impl UsageAgent {
    pub async fn new(config: AgentConfig) -> Result<Self> {
        let db_config = DatabaseConfig {
            path: config.database_path.clone(),
            retention_days: 30,
            turso_url: None,
            turso_auth_token: None,
        };

        let db = UsageDatabase::new(db_config).await?;
        let db = Arc::new(db);

        let config = Arc::new(Mutex::new(config));

        let state = AgentState::new(db, config);

        Ok(Self { state })
    }

    pub async fn run(&self) -> Result<()> {
        log::info!("Starting AI Consumption Tracker Agent");
        let config_guard = self.state.config.lock().await;
        log::info!("Database: {:?}", config_guard.database_path);
        log::info!("Polling interval: {} seconds", config_guard.polling_interval_seconds);

        let polling_service = PollingService::new(
            self.state.clone(),
            Duration::from_secs(config_guard.polling_interval_seconds),
        );

        log::info!("Starting HTTP API server on http://{}", config_guard.listen_address);

        let server = api::start_server(self.state.clone(), config_guard.listen_address.clone());

        drop(config_guard);

        tokio::select! {
            _ = polling_service.run() => {}
            _ = server => {}
        }

        Ok(())
    }
}
