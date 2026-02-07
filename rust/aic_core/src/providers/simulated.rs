use crate::models::{ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;

pub struct SimulatedProvider;

#[async_trait]
impl ProviderService for SimulatedProvider {
    fn provider_id(&self) -> &'static str {
        "simulated"
    }

    async fn get_usage(&self, _config: &ProviderConfig) -> Vec<ProviderUsage> {
        tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;

        vec![ProviderUsage {
            provider_id: self.provider_id().to_string(),
            provider_name: "Simulated Provider".to_string(),
            usage_percentage: 45.5,
            cost_used: 12.50,
            cost_limit: 100.0,
            is_quota_based: true,
            description: "45% Used".to_string(),
            ..Default::default()
        }]
    }
}
