use aic_core::{PaymentType, ProviderConfig, ProviderService, ProviderUsage};
use async_trait::async_trait;

pub struct MockProvider {
    provider_id: &'static str,
    usage_handler: Box<dyn Fn(&ProviderConfig) -> Vec<ProviderUsage> + Send + Sync>,
}

impl MockProvider {
    pub fn new<F>(provider_id: &'static str, handler: F) -> Self
    where
        F: Fn(&ProviderConfig) -> Vec<ProviderUsage> + Send + Sync + 'static,
    {
        Self {
            provider_id,
            usage_handler: Box::new(handler),
        }
    }

    pub fn create_openai_mock() -> Self {
        Self::new("openai", |_config| {
            vec![ProviderUsage {
                provider_id: "openai".to_string(),
                provider_name: "OpenAI".to_string(),
                usage_percentage: 25.0,
                cost_used: 2.5,
                cost_limit: 10.0,
                payment_type: PaymentType::UsageBased,
                usage_unit: "USD".to_string(),
                description: "$2.50 / $10.00 used".to_string(),
                is_available: true,
                ..Default::default()
            }]
        })
    }

    pub fn create_anthropic_mock() -> Self {
        Self::new("anthropic", |_config| {
            vec![ProviderUsage {
                provider_id: "anthropic".to_string(),
                provider_name: "Anthropic".to_string(),
                usage_percentage: 75.0,
                cost_used: 15.0,
                cost_limit: 20.0,
                payment_type: PaymentType::Credits,
                usage_unit: "USD".to_string(),
                description: "$5.00 remaining".to_string(),
                is_available: true,
                ..Default::default()
            }]
        })
    }

    pub fn create_gemini_mock() -> Self {
        Self::new("gemini", |_config| {
            vec![ProviderUsage {
                provider_id: "gemini".to_string(),
                provider_name: "Gemini".to_string(),
                usage_percentage: 10.0,
                cost_used: 150.0,
                cost_limit: 1500.0,
                payment_type: PaymentType::Quota,
                usage_unit: "Requests".to_string(),
                description: "150 / 1500 requests".to_string(),
                is_available: true,
                ..Default::default()
            }]
        })
    }

    pub fn create_gemini_cli_mock() -> Self {
        Self::new("gemini-cli", |_config| {
            vec![ProviderUsage {
                provider_id: "gemini-cli".to_string(),
                provider_name: "Gemini CLI".to_string(),
                usage_percentage: 5.0,
                cost_used: 500.0,
                cost_limit: 10000.0,
                payment_type: PaymentType::Quota,
                usage_unit: "Tokens".to_string(),
                description: "500 / 10,000 tokens".to_string(),
                is_available: true,
                ..Default::default()
            }]
        })
    }

    pub fn create_antigravity_mock() -> Self {
        Self::new("antigravity", |_config| {
            vec![ProviderUsage {
                provider_id: "antigravity".to_string(),
                provider_name: "Antigravity".to_string(),
                usage_percentage: 40.0,
                cost_used: 4.0,
                cost_limit: 10.0,
                payment_type: PaymentType::Credits,
                usage_unit: "USD".to_string(),
                description: "$6.00 remaining".to_string(),
                is_available: true,
                ..Default::default()
            }]
        })
    }

    pub fn create_opencode_zen_mock() -> Self {
        Self::new("opencode-zen", |_config| {
            vec![ProviderUsage {
                provider_id: "opencode-zen".to_string(),
                provider_name: "OpenCode Zen".to_string(),
                usage_percentage: 20.0,
                cost_used: 1.0,
                cost_limit: 5.0,
                payment_type: PaymentType::Quota,
                usage_unit: "Requests".to_string(),
                description: "1 / 5 requests".to_string(),
                is_available: true,
                ..Default::default()
            }]
        })
    }

    pub fn create_generic_mock() -> Self {
        Self::new("generic-pay-as-you-go", |config| {
            vec![ProviderUsage {
                provider_id: config.provider_id.clone(),
                provider_name: "Fallback Provider".to_string(),
                is_available: true,
                description: "Generic Fallback".to_string(),
                ..Default::default()
            }]
        })
    }
}

#[async_trait]
impl ProviderService for MockProvider {
    fn provider_id(&self) -> &'static str {
        self.provider_id
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        (self.usage_handler)(config)
    }
}
