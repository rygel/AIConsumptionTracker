pub mod auth_manager;
pub mod config;
pub mod github_auth;
pub mod models;
pub mod privacy;
pub mod provider;
pub mod providers;

pub use auth_manager::AuthenticationManager;
pub use config::{ConfigLoader, ProviderManager};
pub use github_auth::{DeviceFlowResponse, GitHubAuthService, TokenPollResult};
pub use models::*;
pub use privacy::mask_content;
pub use provider::ProviderService;
pub use providers::*;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_payment_type_default() {
        assert_eq!(PaymentType::default(), PaymentType::UsageBased);
    }

    #[test]
    fn test_provider_usage_default() {
        let usage = ProviderUsage::default();
        assert_eq!(usage.payment_type, PaymentType::UsageBased);
        assert!(!usage.is_quota_based);
        assert!(usage.is_available);
        assert!(usage.description.is_empty());
    }

    #[test]
    fn test_provider_config_default() {
        let config = ProviderConfig::default();
        assert!(config.api_key.is_empty());
        assert_eq!(config.config_type, "pay-as-you-go");
        assert_eq!(config.limit, Some(100.0));
    }

    #[test]
    fn test_app_preferences_default() {
        let prefs = AppPreferences::default();
        assert!(!prefs.show_all);
        assert_eq!(prefs.window_width, 420.0);
        assert_eq!(prefs.window_height, 500.0);
        assert!(prefs.always_on_top);
    }
}
