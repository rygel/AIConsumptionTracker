use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;
use log::error;
use reqwest::Client;
use serde::Deserialize;

pub struct XiaomiProvider {
    client: Client,
}

impl XiaomiProvider {
    pub fn new(client: Client) -> Self {
        Self { client }
    }
}

#[derive(Debug, Deserialize)]
struct XiaomiResponse {
    data: Option<XiaomiData>,
    code: Option<i32>,
}

#[derive(Debug, Deserialize)]
struct XiaomiData {
    balance: f64,
    quota: f64,
}

#[async_trait]
impl ProviderService for XiaomiProvider {
    fn provider_id(&self) -> &'static str {
        "xiaomi"
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        if config.api_key.is_empty() {
            return vec![ProviderUsage {
                provider_id: config.provider_id.clone(),
                provider_name: "Xiaomi".to_string(),
                is_available: false,
                description: "API Key missing".to_string(),
                ..Default::default()
            }];
        }

        let url = "https://api.xiaomimimo.com/v1/user/balance";

        match self
            .client
            .get(url)
            .header("Authorization", format!("Bearer {}", config.api_key))
            .send()
            .await
        {
            Ok(response) => {
                if !response.status().is_success() {
                    return vec![ProviderUsage {
                        provider_id: config.provider_id.clone(),
                        provider_name: "Xiaomi".to_string(),
                        is_available: false,
                        description: format!("API Error ({})", response.status()),
                        ..Default::default()
                    }];
                }

                let response_string = match response.text().await {
                    Ok(s) => s,
                    Err(e) => {
                        error!("Failed to read Xiaomi response: {}", e);
                        return vec![ProviderUsage {
                            provider_id: config.provider_id.clone(),
                            provider_name: "Xiaomi".to_string(),
                            is_available: false,
                            description: "Failed to read response".to_string(),
                            ..Default::default()
                        }];
                    }
                };

                match serde_json::from_str::<XiaomiResponse>(&response_string) {
                    Ok(data) => {
                        if data.code == Some(0) && data.data.is_some() {
                            let xiaomi_data = data.data.unwrap();
                            let balance = xiaomi_data.balance;
                            let quota = xiaomi_data.quota;

                            let percentage = if quota > 0.0 {
                                ((quota - balance) / quota) * 100.0
                            } else {
                                0.0
                            };

                            vec![ProviderUsage {
                                provider_id: config.provider_id.clone(),
                                provider_name: "Xiaomi".to_string(),
                                usage_percentage: percentage.min(100.0),
                                cost_used: if quota > 0.0 { quota - balance } else { 0.0 },
                                cost_limit: if quota > 0.0 { quota } else { balance },
                                usage_unit: "Points".to_string(),
                                is_quota_based: quota > 0.0,
                                payment_type: if quota > 0.0 {
                                    PaymentType::Quota
                                } else {
                                    PaymentType::UsageBased
                                },
                                is_available: true,
                                description: if quota > 0.0 {
                                    format!("{:.2} remaining / {:.2} total", balance, quota)
                                } else {
                                    format!("Balance: {:.2}", balance)
                                },
                                ..Default::default()
                            }]
                        } else {
                            vec![ProviderUsage {
                                provider_id: config.provider_id.clone(),
                                provider_name: "Xiaomi".to_string(),
                                is_available: false,
                                description: "Invalid response from Xiaomi API".to_string(),
                                ..Default::default()
                            }]
                        }
                    }
                    Err(e) => {
                        error!("Failed to parse Xiaomi response: {}", e);
                        vec![ProviderUsage {
                            provider_id: config.provider_id.clone(),
                            provider_name: "Xiaomi".to_string(),
                            is_available: false,
                            description: format!("Parse error: {}", e),
                            ..Default::default()
                        }]
                    }
                }
            }
            Err(e) => {
                error!("Xiaomi API request failed: {}", e);
                vec![ProviderUsage {
                    provider_id: config.provider_id.clone(),
                    provider_name: "Xiaomi".to_string(),
                    is_available: false,
                    description: "Connection Failed".to_string(),
                    ..Default::default()
                }]
            }
        }
    }
}
