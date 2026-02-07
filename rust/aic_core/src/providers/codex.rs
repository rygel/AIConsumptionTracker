use crate::models::{ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;

pub struct CodexProvider;

#[async_trait]
impl ProviderService for CodexProvider {
    fn provider_id(&self) -> &'static str {
        "codex"
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        if config.api_key.is_empty() {
            return vec![];
        }

        vec![ProviderUsage {
            provider_id: self.provider_id().to_string(),
            provider_name: "Codex".to_string(),
            is_available: true,
            description: "Codex usage tracking (Implementation pending specific API details)"
                .to_string(),
            ..Default::default()
        }]
    }
}
