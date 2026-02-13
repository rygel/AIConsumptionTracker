use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;
use chrono::{DateTime, Utc};
use reqwest::Client;
use serde::Deserialize;

pub struct SyntheticProvider {
    client: Client,
}

impl SyntheticProvider {
    pub fn new(client: Client) -> Self {
        Self { client }
    }
}

#[derive(Debug, Deserialize)]
struct SyntheticResponse {
    subscription: Option<SyntheticSubscription>,
}

#[derive(Debug, Deserialize)]
struct SyntheticSubscription {
    limit: f64,
    requests: f64,
    #[serde(rename = "renewsAt")]
    renews_at: Option<String>,
}

#[async_trait]
impl ProviderService for SyntheticProvider {
    fn provider_id(&self) -> &'static str {
        "synthetic"
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        if config.api_key.is_empty() {
            return vec![ProviderUsage {
                provider_id: self.provider_id().to_string(),
                provider_name: "Synthetic".to_string(),
                is_available: false,
                description: "API Key not found".to_string(),
                ..Default::default()
            }];
        }

        // Default URL for Synthetic
        let url = config
            .base_url
            .clone()
            .unwrap_or_else(|| "https://api.synthitic.ai/v1/usage".to_string());

        match self
            .client
            .get(&url)
            .header("Authorization", &config.api_key)
            .send()
            .await
        {
            Ok(response) => {
                if !response.status().is_success() {
                    return vec![ProviderUsage {
                        provider_id: self.provider_id().to_string(),
                        provider_name: "Synthetic".to_string(),
                        is_available: false,
                        description: format!("API Error ({})", response.status()),
                        ..Default::default()
                    }];
                }

                match response.json::<SyntheticResponse>().await {
                    Ok(data) => {
                        if let Some(sub) = data.subscription {
                            let total = sub.limit;
                            let used = sub.requests;
                            
                            let utilization = if total > 0.0 {
                                (used / total) * 100.0
                            } else {
                                0.0
                            };
                            
                            let remaining_percent = 100.0 - utilization.min(100.0);
                            
                            let next_reset_time = sub.renews_at.and_then(|renews_at| {
                                DateTime::parse_from_rfc3339(&renews_at)
                                    .ok()
                                    .map(|dt| dt.with_timezone(&Utc))
                            });

                            vec![ProviderUsage {
                                provider_id: self.provider_id().to_string(),
                                provider_name: "Synthetic".to_string(),
                                usage_percentage: utilization.min(100.0),
                                remaining_percentage: Some(remaining_percent),
                                cost_used: used,
                                cost_limit: total,
                                payment_type: PaymentType::Quota,
                                usage_unit: "Quota %".to_string(),
                                is_quota_based: true,
                                description: format!("{:.1}% used", utilization),
                                next_reset_time,
                                ..Default::default()
                            }]
                        } else {
                            vec![ProviderUsage {
                                provider_id: self.provider_id().to_string(),
                                provider_name: "Synthetic".to_string(),
                                is_available: false,
                                description: "No subscription data found".to_string(),
                                ..Default::default()
                            }]
                        }
                    }
                    Err(_) => {
                        vec![ProviderUsage {
                            provider_id: self.provider_id().to_string(),
                            provider_name: "Synthetic".to_string(),
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
                    provider_name: "Synthetic".to_string(),
                    is_available: false,
                    description: "Connection Failed".to_string(),
                    ..Default::default()
                }]
            }
        }
    }
}
