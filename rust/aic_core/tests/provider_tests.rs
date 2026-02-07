use aic_core::{
    AnthropicProvider, DeepSeekProvider, OpenAIProvider, PaymentType, ProviderConfig,
    ProviderService, ProviderUsage,
};
use reqwest::Client;

#[tokio::test]
async fn openai_provider_returns_error_for_missing_api_key() {
    // Arrange
    let client = Client::new();
    let provider = OpenAIProvider::new(client);
    let config = ProviderConfig {
        provider_id: "openai".to_string(),
        api_key: "".to_string(),
        ..Default::default()
    };

    // Act
    let usage: Vec<aic_core::ProviderUsage> = provider.get_usage(&config).await;

    // Assert
    assert_eq!(usage.len(), 1);
    assert!(!usage[0].is_available);
    assert!(usage[0].description.contains("missing"));
}

#[tokio::test]
async fn openai_provider_rejects_project_keys() {
    // Arrange
    let client = Client::new();
    let provider = OpenAIProvider::new(client);
    let config = ProviderConfig {
        provider_id: "openai".to_string(),
        api_key: "sk-proj-1234567890".to_string(),
        ..Default::default()
    };

    // Act
    let usage: Vec<aic_core::ProviderUsage> = provider.get_usage(&config).await;

    // Assert
    assert_eq!(usage.len(), 1);
    assert!(!usage[0].is_available);
    assert!(usage[0].description.contains("not supported"));
}

#[tokio::test]
async fn anthropic_provider_returns_error_for_missing_api_key() {
    // Arrange
    let provider = AnthropicProvider;
    let config = ProviderConfig {
        provider_id: "anthropic".to_string(),
        api_key: "".to_string(),
        ..Default::default()
    };

    // Act
    let usage: Vec<aic_core::ProviderUsage> = provider.get_usage(&config).await;

    // Assert
    assert_eq!(usage.len(), 1);
    assert!(!usage[0].is_available);
    assert!(usage[0].description.contains("missing"));
}

#[tokio::test]
async fn anthropic_provider_returns_connected_with_api_key() {
    // Arrange
    let provider = AnthropicProvider;
    let config = ProviderConfig {
        provider_id: "anthropic".to_string(),
        api_key: "sk-ant-test".to_string(),
        ..Default::default()
    };

    // Act
    let usage: Vec<aic_core::ProviderUsage> = provider.get_usage(&config).await;

    // Assert
    assert_eq!(usage.len(), 1);
    assert!(usage[0].is_available);
    assert_eq!(usage[0].payment_type, PaymentType::UsageBased);
    assert_eq!(usage[0].usage_unit, "Status");
}

#[tokio::test]
async fn deepseek_provider_returns_error_for_missing_api_key() {
    // Arrange
    let client = Client::new();
    let provider = DeepSeekProvider::new(client);
    let config = ProviderConfig {
        provider_id: "deepseek".to_string(),
        api_key: "".to_string(),
        ..Default::default()
    };

    // Act
    let usage: Vec<aic_core::ProviderUsage> = provider.get_usage(&config).await;

    // Assert
    assert_eq!(usage.len(), 1);
    assert!(!usage[0].is_available);
    assert!(usage[0].description.contains("missing"));
}

#[tokio::test]
async fn provider_usage_default_values() {
    // Arrange & Act
    let usage = ProviderUsage::default();

    // Assert
    assert_eq!(usage.usage_unit, "USD");
    assert!(!usage.is_quota_based);
    assert!(usage.is_available);
    assert!(usage.provider_id.is_empty());
    assert!(usage.provider_name.is_empty());
    assert_eq!(usage.usage_percentage, 0.0);
    assert_eq!(usage.cost_used, 0.0);
    assert_eq!(usage.cost_limit, 0.0);
}

#[tokio::test]
async fn provider_config_payment_type_defaults_to_usage_based() {
    // Arrange & Act
    let config = ProviderConfig::default();

    // Assert
    assert_eq!(config.payment_type, PaymentType::UsageBased);
}

#[tokio::test]
async fn provider_config_limit_has_default_value() {
    // Arrange & Act
    let config = ProviderConfig::default();

    // Assert
    assert_eq!(config.limit, Some(100.0));
}
