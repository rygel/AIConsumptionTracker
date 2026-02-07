use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;
use log::error;
use reqwest::Client;
use serde::Deserialize;

pub struct DeepSeekProvider {
    client: Client,
}

impl DeepSeekProvider {
    pub fn new(client: Client) -> Self {
        Self { client }
    }
}

#[derive(Debug, Deserialize)]
struct DeepSeekBalanceResponse {
    #[serde(rename = "is_available")]
    is_available: bool,
    #[serde(rename = "balance_infos")]
    balance_infos: Option<Vec<BalanceInfo>>,
}

#[derive(Debug, Deserialize)]
struct BalanceInfo {
    currency: String,
    #[serde(rename = "total_balance")]
    total_balance: f64,
    #[serde(rename = "granted_balance")]
    #[allow(dead_code)]
    granted_balance: f64,
    #[serde(rename = "topped_up_balance")]
    #[allow(dead_code)]
    topped_up_balance: f64,
}

#[async_trait]
impl ProviderService for DeepSeekProvider {
    fn provider_id(&self) -> &'static str {
        "deepseek"
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        if config.api_key.is_empty() {
            return vec![ProviderUsage {
                provider_id: self.provider_id().to_string(),
                provider_name: "DeepSeek".to_string(),
                is_available: false,
                description: "API Key missing".to_string(),
                ..Default::default()
            }];
        }

        match self
            .client
            .get("https://api.deepseek.com/user/balance")
            .header("Authorization", format!("Bearer {}", config.api_key))
            .header("Accept", "application/json")
            .send()
            .await
        {
            Ok(response) => {
                if !response.status().is_success() {
                    let status = response.status();
                    return vec![ProviderUsage {
                        provider_id: self.provider_id().to_string(),
                        provider_name: "DeepSeek".to_string(),
                        is_available: true,
                        description: format!("API Error ({})", status),
                        usage_percentage: 0.0,
                        is_quota_based: false,
                        ..Default::default()
                    }];
                }

                match response.json::<DeepSeekBalanceResponse>().await {
                    Ok(result) => {
                        if !result.is_available {
                            return vec![ProviderUsage {
                                provider_id: self.provider_id().to_string(),
                                provider_name: "DeepSeek".to_string(),
                                is_available: false,
                                description: "Account unavailable or parsing failed".to_string(),
                                ..Default::default()
                            }];
                        }

                        if let Some(balance_infos) = result.balance_infos {
                            if let Some(main_balance) = balance_infos.first() {
                                let currency_symbol = if main_balance.currency == "CNY" {
                                    "¥"
                                } else {
                                    "$"
                                };
                                let balance_text =
                                    format!("{}{:.2}", currency_symbol, main_balance.total_balance);

                                return vec![ProviderUsage {
                                    provider_id: self.provider_id().to_string(),
                                    provider_name: "DeepSeek".to_string(),
                                    is_available: true,
                                    usage_percentage: 0.0,
                                    cost_used: 0.0,
                                    cost_limit: 0.0,
                                    usage_unit: "Currency".to_string(),
                                    is_quota_based: false,
                                    payment_type: PaymentType::Credits,
                                    description: format!("Balance: {}", balance_text),
                                    ..Default::default()
                                }];
                            }
                        }

                        vec![ProviderUsage {
                            provider_id: self.provider_id().to_string(),
                            provider_name: "DeepSeek".to_string(),
                            is_available: true,
                            description: "No balance info found".to_string(),
                            ..Default::default()
                        }]
                    }
                    Err(e) => {
                        error!("Failed to parse DeepSeek response: {}", e);
                        vec![ProviderUsage {
                            provider_id: self.provider_id().to_string(),
                            provider_name: "DeepSeek".to_string(),
                            is_available: false,
                            description: "Parsing failed".to_string(),
                            ..Default::default()
                        }]
                    }
                }
            }
            Err(e) => {
                error!("DeepSeek check failed: {}", e);
                vec![ProviderUsage {
                    provider_id: self.provider_id().to_string(),
                    provider_name: "DeepSeek".to_string(),
                    is_available: false,
                    description: "Check failed".to_string(),
                    ..Default::default()
                }]
            }
        }
    }
}
