use crate::models::{PaymentType, ProviderConfig, ProviderUsage};
use crate::provider::ProviderService;
use async_trait::async_trait;
use log::warn;
use regex::Regex;
use std::process::Stdio;
use std::time::Duration;
use tokio::process::Command;
use tokio::time::timeout;

pub struct OpenCodeZenProvider {
    cli_path: String,
}

impl OpenCodeZenProvider {
    pub fn new() -> Self {
        // Default path - should be configurable in real app
        let cli_path = if cfg!(windows) {
            r"C:\Users\Alexander\AppData\Roaming\npm\opencode.cmd".to_string()
        } else {
            "opencode".to_string()
        };

        Self { cli_path }
    }

    pub fn with_path(path: String) -> Self {
        Self { cli_path: path }
    }

    async fn run_cli(&self) -> Result<String, Box<dyn std::error::Error>> {
        // Check if CLI exists
        if !std::path::Path::new(&self.cli_path).exists() && !self.cli_path.eq("opencode") {
            return Err(format!("CLI not found at: {}", self.cli_path).into());
        }

        let mut cmd = Command::new(&self.cli_path);
        cmd.args(["stats", "--days", "7", "--models", "10"])
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .kill_on_drop(true);

        let output = timeout(Duration::from_secs(5), cmd.output()).await??;

        if !output.status.success() {
            let stderr = String::from_utf8_lossy(&output.stderr);
            return Err(format!("CLI Error: {} - {}", output.status, stderr).into());
        }

        Ok(String::from_utf8_lossy(&output.stdout).to_string())
    }

    fn parse_output(&self, output: &str) -> ProviderUsage {
        // Parse patterns like:
        // │Total Cost   $12.34
        // │Avg Cost/Day $1.23
        // │Sessions     123

        let mut total_cost: f64 = 0.0;
        let mut _avg_cost: f64 = 0.0;

        // Clean ANSI codes (simplified - remove common escape sequences)
        let cleaned = output
            .replace("\u{001b}[", "")
            .replace("0m", "")
            .replace("1m", "")
            .replace("32m", "")
            .replace("36m", "")
            .replace("90m", "");

        // Parse Total Cost
        let cost_re = Regex::new(r"Total Cost\s+\$([0-9.]+)").unwrap();
        if let Some(caps) = cost_re.captures(&cleaned) {
            if let Some(cost_match) = caps.get(1) {
                total_cost = cost_match.as_str().parse().unwrap_or(0.0);
            }
        }

        // Parse Avg Cost/Day
        let avg_re = Regex::new(r"Avg Cost/Day\s+\$([0-9.]+)").unwrap();
        if let Some(caps) = avg_re.captures(&cleaned) {
            if let Some(avg_match) = caps.get(1) {
                _avg_cost = avg_match.as_str().parse().unwrap_or(0.0);
            }
        }

        ProviderUsage {
            provider_id: "opencode-zen".to_string(),
            provider_name: "OpenCode Zen".to_string(),
            usage_percentage: 0.0, // Pay as you go, no limit
            cost_used: total_cost,
            cost_limit: 0.0,
            usage_unit: "USD".to_string(),
            is_quota_based: false,
            payment_type: PaymentType::UsageBased,
            is_available: true,
            description: format!("${:.2} (7 days)", total_cost),
            ..Default::default()
        }
    }
}

#[async_trait]
impl ProviderService for OpenCodeZenProvider {
    fn provider_id(&self) -> &'static str {
        "opencode-zen"
    }

    async fn get_usage(&self, _config: &ProviderConfig) -> Vec<ProviderUsage> {
        // Check if CLI exists first
        let path_exists = if self.cli_path.eq("opencode") {
            // Try to find in PATH
            which::which("opencode").is_ok()
        } else {
            std::path::Path::new(&self.cli_path).exists()
        };

        if !path_exists {
            return vec![ProviderUsage {
                provider_id: self.provider_id().to_string(),
                provider_name: "OpenCode Zen".to_string(),
                is_available: false,
                description: "CLI not found at expected path".to_string(),
                ..Default::default()
            }];
        }

        match self.run_cli().await {
            Ok(output) => {
                vec![self.parse_output(&output)]
            }
            Err(e) => {
                warn!("OpenCode CLI failed: {}", e);
                vec![ProviderUsage {
                    provider_id: self.provider_id().to_string(),
                    provider_name: "OpenCode Zen".to_string(),
                    is_available: false,
                    description: format!(
                        "CLI Error: {} (Check log or clear storage if JSON error)",
                        e
                    ),
                    ..Default::default()
                }]
            }
        }
    }
}
