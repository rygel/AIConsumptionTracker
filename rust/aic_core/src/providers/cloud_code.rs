use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;

pub struct CloudCodeProvider;

#[async_trait]
impl ProviderService for CloudCodeProvider {
    fn provider_id(&self) -> &'static str {
        "cloud-code"
    }

    async fn get_usage(&self, config: &ProviderConfig) -> Vec<ProviderUsage> {
        let mut is_connected = false;
        let mut message = "Not connected".to_string();

        if !config.api_key.is_empty() {
            is_connected = true;
            message = "Configured (Key present)".to_string();
        } else {
            // Try gcloud check
            match std::process::Command::new("gcloud")
                .args(["auth", "print-access-token"])
                .stdout(std::process::Stdio::piped())
                .stderr(std::process::Stdio::piped())
                .output()
            {
                Ok(output) => {
                    if output.status.success() {
                        is_connected = true;
                        message = "Connected (gcloud)".to_string();
                    } else {
                        let error = String::from_utf8_lossy(&output.stderr);
                        message = format!("gcloud Error: {}", error.trim());
                    }
                }
                Err(_) => {
                    message = "gcloud not found".to_string();
                }
            }
        }

        vec![ProviderUsage {
            provider_id: self.provider_id().to_string(),
            provider_name: "Cloud Code (Google)".to_string(),
            is_available: is_connected,
            usage_percentage: 0.0,
            is_quota_based: false,
            payment_type: PaymentType::UsageBased,
            description: message,
            usage_unit: "Status".to_string(),
            ..Default::default()
        }]
    }
}
