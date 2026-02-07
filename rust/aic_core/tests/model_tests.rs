use aic_core::{AppPreferences, PaymentType, ProviderConfig, ProviderUsage};

#[test]
fn provider_usage_initialization_sets_default_values() {
    // Arrange & Act
    let usage = ProviderUsage::default();

    // Assert
    assert_eq!(usage.payment_type, PaymentType::UsageBased);
    assert!(!usage.is_quota_based);
    assert!(usage.is_available);
    assert!(usage.description.is_empty());
    assert_eq!(usage.usage_unit, "USD");
}

#[test]
fn provider_config_initialization_sets_default_values() {
    // Arrange & Act
    let config = ProviderConfig::default();

    // Assert
    assert!(config.api_key.is_empty());
    assert_eq!(config.config_type, "pay-as-you-go");
    assert_eq!(config.payment_type, PaymentType::UsageBased);
    assert_eq!(config.limit, Some(100.0));
}

#[test]
fn app_preferences_initialization_sets_default_values() {
    // Arrange & Act
    let prefs = AppPreferences::default();

    // Assert
    assert!(!prefs.show_all);
    assert_eq!(prefs.window_width, 420.0);
    assert_eq!(prefs.window_height, 500.0);
    assert!(!prefs.stay_open);
    assert!(prefs.always_on_top);
    assert!(prefs.compact_mode);
    assert_eq!(prefs.color_threshold_yellow, 60);
    assert_eq!(prefs.color_threshold_red, 80);
    assert!(!prefs.invert_progress_bar);
    assert_eq!(prefs.font_family, "Segoe UI");
    assert_eq!(prefs.font_size, 12);
    assert!(!prefs.font_bold);
    assert!(!prefs.font_italic);
}

#[test]
fn provider_usage_serialization_roundtrip() {
    // Arrange
    let usage = ProviderUsage {
        provider_id: "test-provider".to_string(),
        provider_name: "Test Provider".to_string(),
        usage_percentage: 50.0,
        cost_used: 25.0,
        cost_limit: 50.0,
        payment_type: PaymentType::Credits,
        usage_unit: "USD".to_string(),
        is_quota_based: true,
        is_available: true,
        description: "Test description".to_string(),
        auth_source: "Test Source".to_string(),
        ..Default::default()
    };

    // Act
    let json = serde_json::to_string(&usage).expect("Failed to serialize");
    let deserialized: ProviderUsage = serde_json::from_str(&json).expect("Failed to deserialize");

    // Assert
    assert_eq!(deserialized.provider_id, usage.provider_id);
    assert_eq!(deserialized.provider_name, usage.provider_name);
    assert_eq!(deserialized.usage_percentage, usage.usage_percentage);
    assert_eq!(deserialized.payment_type, usage.payment_type);
    assert_eq!(deserialized.is_quota_based, usage.is_quota_based);
}

#[test]
fn provider_config_serialization_roundtrip() {
    // Arrange
    let config = ProviderConfig {
        provider_id: "openai".to_string(),
        api_key: "sk-test".to_string(),
        config_type: "api".to_string(),
        payment_type: PaymentType::UsageBased,
        limit: Some(100.0),
        base_url: Some("https://api.openai.com".to_string()),
        show_in_tray: true,
        enabled_sub_trays: vec!["sub1".to_string(), "sub2".to_string()],
        auth_source: "Test".to_string(),
        description: Some("Test description".to_string()),
    };

    // Act
    let json = serde_json::to_string(&config).expect("Failed to serialize");
    let deserialized: ProviderConfig = serde_json::from_str(&json).expect("Failed to deserialize");

    // Assert
    assert_eq!(deserialized.provider_id, config.provider_id);
    assert_eq!(deserialized.api_key, config.api_key);
    assert_eq!(deserialized.config_type, config.config_type);
    assert_eq!(deserialized.base_url, config.base_url);
}
