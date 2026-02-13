use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;
use reqwest::Client;
use serde::Deserialize;

/// MiniMax China provider
/// API endpoint: https://api.minimax.chat/v1/user/usage
pub struct MinimaxProvider {
    client: Client,
}

impl MinimaxProvider {
    pub fn new(client: Client) -> Self {
        Self { client }
    }
}

#[derive(Debug, Deserialize)]
struct MinimaxResponse {
    usage: Option<MinimaxUsage>,
}

#[derive(Debug, Deserialize)]
struct MinimaxUsage {
    #[serde(rename = "tokensUsed")]
    tokens_used: f64,
    #[serde(rename = "tokensLimit")]
    tokens_limit: f64,
}

#[async_trait]
impl ProviderService for MinimaxProvider {
    fn provider_id(&self) -> &'static str {
        "minimax"
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        if config.api_key.is_empty() {
            return vec![ProviderUsage {
                provider_id: self.provider_id().to_string(),
                provider_name: "MiniMax (China)".to_string(),
                is_available: false,
                description: "API Key missing".to_string(),
                ..Default::default()
            }];
        }

        // Use custom base_url if provided, otherwise use China endpoint
        let url = config
            .base_url
            .clone()
            .unwrap_or_else(|| "https://api.minimax.chat/v1/user/usage".to_string());

        match self
            .client
            .get(&url)
            .header("Authorization", format!("Bearer {}", config.api_key))
            .send()
            .await
        {
            Ok(response) => {
                if !response.status().is_success() {
                    return vec![ProviderUsage {
                        provider_id: self.provider_id().to_string(),
                        provider_name: "MiniMax (China)".to_string(),
                        is_available: false,
                        description: format!("API Error ({})", response.status()),
                        ..Default::default()
                    }];
                }

                match response.json::<MinimaxResponse>().await {
                    Ok(data) => {
                        if let Some(usage) = data.usage {
                            let used = usage.tokens_used;
                            let total = usage.tokens_limit;
                            let utilization = if total > 0.0 {
                                (used / total) * 100.0
                            } else {
                                0.0
                            };

                            vec![ProviderUsage {
                                provider_id: self.provider_id().to_string(),
                                provider_name: "MiniMax (China)".to_string(),
                                usage_percentage: utilization.min(100.0),
                                cost_used: used,
                                cost_limit: total,
                                payment_type: PaymentType::UsageBased,
                                usage_unit: "Tokens".to_string(),
                                is_quota_based: false,
                                description: format!(
                                    "{} tokens used{}",
                                    format_tokens(used),
                                    if total > 0.0 {
                                        format!(" / {} limit", format_tokens(total))
                                    } else {
                                        String::new()
                                    }
                                ),
                                ..Default::default()
                            }]
                        } else {
                            vec![ProviderUsage {
                                provider_id: self.provider_id().to_string(),
                                provider_name: "MiniMax (China)".to_string(),
                                is_available: false,
                                description: "Invalid response format".to_string(),
                                ..Default::default()
                            }]
                        }
                    }
                    Err(_) => {
                        vec![ProviderUsage {
                            provider_id: self.provider_id().to_string(),
                            provider_name: "MiniMax (China)".to_string(),
                            is_available: false,
                            description: "Failed to parse response".to_string(),
                            ..Default::default()
                        }]
                    }
                }
            }
            Err(_) => {
                vec![ProviderUsage {
                    provider_id: self.provider_id().to_string(),
                    provider_name: "MiniMax (China)".to_string(),
                    is_available: false,
                    description: "Connection Failed".to_string(),
                    ..Default::default()
                }]
            }
        }
    }
}

fn format_tokens(tokens: f64) -> String {
    if tokens >= 1_000_000.0 {
        format!("{:.1}M", tokens / 1_000_000.0)
    } else if tokens >= 1_000.0 {
        format!("{:.1}K", tokens / 1_000.0)
    } else {
        format!("{:.0}", tokens)
    }
}
