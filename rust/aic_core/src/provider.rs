use crate::models::{ProviderConfig, ProviderUsage};
use async_trait::async_trait;

#[async_trait]
pub trait ProviderService: Send + Sync {
    fn provider_id(&self) -> &'static str;
    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage>;
}
