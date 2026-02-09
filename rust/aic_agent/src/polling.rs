use std::time::Duration;
use tokio::time;
use log::info;

use aic_core::{ConfigLoader, PaymentType, ProviderUsage};
use reqwest::Client;

use super::AgentState;

pub struct PollingService {
    state: AgentState,
    interval: Duration,
}

impl PollingService {
    pub fn new(state: AgentState, interval: Duration) -> Self {
        Self { state, interval }
    }

    pub async fn run(&self) {
        info!("Polling service started with interval: {} seconds", self.interval.as_secs());

        let mut interval = time::interval(self.interval);

        loop {
            interval.tick().await;

            let config = self.state.config.lock().await;
            if !config.polling_enabled {
                info!("Polling is disabled, skipping...");
                continue;
            }
            drop(config);

            info!("Starting scheduled refresh...");

            match self.collect_all().await {
                Ok(results) => {
                    info!("Refresh completed. Collected {} provider updates", results.len());
                }
                Err(e) => {
                    log::error!("Refresh failed: {}", e);
                }
            }
        }
    }

    async fn collect_all(&self) -> Result<Vec<ProviderUsage>, Box<dyn std::error::Error>> {
        let client = Client::new();
        let config_loader = ConfigLoader::new(client);
        let configs = config_loader.load_config().await;

        let mut all_usages = Vec::new();

        for config in configs {
            let usage = ProviderUsage {
                provider_id: config.provider_id.clone(),
                provider_name: config.provider_id.clone(),
                usage_percentage: 0.0,
                cost_used: 0.0,
                cost_limit: config.limit.unwrap_or(100.0),
                payment_type: PaymentType::UsageBased,
                usage_unit: "unknown".to_string(),
                is_quota_based: false,
                is_available: false,
                description: format!("Configured: {}", config.provider_id),
                auth_source: config.auth_source,
                details: None,
                account_name: "unknown".to_string(),
                next_reset_time: None,
            };
            all_usages.push(usage);
        }

        if !all_usages.is_empty() {
            self.state.db.store_usage(all_usages.clone()).await?;
        }

        Ok(all_usages)
    }
}
