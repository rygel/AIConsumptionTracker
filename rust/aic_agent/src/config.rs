use serde::{Deserialize, Serialize};
use std::path::PathBuf;

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct AgentConfig {
    pub database_path: PathBuf,
    pub listen_address: String,
    pub polling_enabled: bool,
    pub polling_interval_seconds: u64,
    pub log_level: String,
}

impl Default for AgentConfig {
    fn default() -> Self {
        let base_dir = directories::BaseDirs::new()
            .map(|base| base.home_dir().join(".ai-consumption-tracker"))
            .unwrap_or_else(|| PathBuf::from(".ai-consumption-tracker"));

        Self {
            database_path: base_dir.join("usage.db"),
            listen_address: "127.0.0.1:8080".to_string(),
            polling_enabled: true,
            polling_interval_seconds: 300,
            log_level: "info".to_string(),
        }
    }
}

impl AgentConfig {
    pub fn load() -> Result<Self, Box<dyn std::error::Error>> {
        let config_path = Self::config_path();

        if config_path.exists() {
            let content = std::fs::read_to_string(&config_path)?;
            toml::from_str(&content).map_err(|e| e.into())
        } else {
            Ok(Self::default())
        }
    }

    pub fn save(&self) -> Result<(), Box<dyn std::error::Error>> {
        let config_path = Self::config_path();

        if let Some(parent) = config_path.parent() {
            std::fs::create_dir_all(parent)?;
        }

        let content = toml::to_string_pretty(self)?;
        std::fs::write(&config_path, content)?;

        Ok(())
    }

    fn config_path() -> PathBuf {
        directories::BaseDirs::new()
            .map(|base| {
                base.config_dir()
                    .join("ai-consumption-tracker")
                    .join("agent.toml")
            })
            .unwrap_or_else(|| PathBuf::from("agent.toml"))
    }
}
