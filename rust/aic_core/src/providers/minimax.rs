use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;
use log::error;
use reqwest::Client;
use serde::Deserialize;

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
    data: Option<MinimaxData>,
}

#[derive(Debug, Deserialize)]
struct MinimaxData {
    #[serde(rename = "tokens_used")]
    tokens_used: f64,
    #[serde(rename = "tokens_limit")]
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
                provider_id: config.provider_id.clone(),
                provider_name: "Minimax".to_string(),
                is_available: false,
                description: "API Key missing".to_string(),
                ..Default::default()
            }];
        }

        let url = if let Some(base_url) = &config.base_url {
            let mut url = base_url.clone();
            if !url.starts_with("http") {
                url = format!("https://{}", url);
            }
            url
        } else if config.provider_id.ends_with("-io")
            || config.provider_id.ends_with("-global")
        {
            "https://api.minimax.io/v1/user/usage".to_string()
        } else {
            "https://api.minimax.chat/v1/user/usage".to_string()
        };

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
                        provider_id: config.provider_id.clone(),
                        provider_name: "Minimax".to_string(),
                        is_available: false,
                        description: format!("API Error ({})", response.status()),
                        ..Default::default()
                    }];
                }

                let response_string = match response.text().await {
                    Ok(s) => s,
                    Err(e) => {
                        error!("Failed to read Minimax response: {}", e);
                        return vec![ProviderUsage {
                            provider_id: config.provider_id.clone(),
                            provider_name: "Minimax".to_string(),
                            is_available: false,
                            description: "Failed to read response".to_string(),
                            ..Default::default()
                        }];
                    }
                };

                match serde_json::from_str::<MinimaxResponse>(&response_string) {
                    Ok(data) => {
                        if let Some(usage) = data.data {
                            let used = usage.tokens_used;
                            let total = if usage.tokens_limit > 0.0 {
                                usage.tokens_limit
                            } else {
                                0.0
                            };

                            let percentage = if total > 0.0 {
                                (used / total) * 100.0
                            } else {
                                0.0
                            };

                            vec![ProviderUsage {
                                provider_id: config.provider_id.clone(),
                                provider_name: "Minimax".to_string(),
                                usage_percentage: percentage.min(100.0),
                                cost_used: used,
                                cost_limit: total,
                                payment_type: PaymentType::UsageBased,
                                usage_unit: "Tokens".to_string(),
                                is_quota_based: false,
                                is_available: true,
                                description: if total > 0.0 {
                                    format!("{:.0} tokens used / {:.0} limit", used, total)
                                } else {
                                    format!("{:.0} tokens used", used)
                                },
                                ..Default::default()
                            }]
                        } else {
                            vec![ProviderUsage {
                                provider_id: config.provider_id.clone(),
                                provider_name: "Minimax".to_string(),
                                is_available: false,
                                description: "Invalid response format".to_string(),
                                ..Default::default()
                            }]
                        }
                    }
                    Err(e) => {
                        error!("Failed to parse Minimax response: {}", e);
                        vec![ProviderUsage {
                            provider_id: config.provider_id.clone(),
                            provider_name: "Minimax".to_string(),
                            is_available: false,
                            description: format!("Parse error: {}", e),
                            ..Default::default()
                        }]
                    }
                }
            }
            Err(e) => {
                error!("Minimax API request failed: {}", e);
                vec![ProviderUsage {
                    provider_id: config.provider_id.clone(),
                    provider_name: "Minimax".to_string(),
                    is_available: false,
                    description: "Connection Failed".to_string(),
                    ..Default::default()
                }]
            }
        }
    }
}
