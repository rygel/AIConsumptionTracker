use aic_app::commands::{AppState, DeviceFlowState};
use aic_core::{AuthenticationManager, ConfigLoader, GitHubAuthService, ProviderManager};
use std::sync::Arc;
use tempfile::TempDir;
use tokio::sync::{Mutex, RwLock};

fn create_test_state() -> (TempDir, AppState) {
    let temp_dir = TempDir::new().unwrap();
    let client = reqwest::Client::new();
    let config_loader = Arc::new(ConfigLoader::with_custom_path(
        client.clone(),
        temp_dir.path().to_path_buf(),
    ));
    let provider_manager = Arc::new(ProviderManager::new(client.clone()));
    let auth_service = Arc::new(GitHubAuthService::new(client.clone()));
    let auth_manager = Arc::new(AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    let state = AppState {
        provider_manager,
        config_loader,
        auth_manager,
        auto_refresh_enabled: Arc::new(Mutex::new(false)),
        device_flow_state: Arc::new(RwLock::new(None)),
    };

    (temp_dir, state)
}

#[tokio::test]
async fn test_app_state_creation() {
    let (_temp_dir, state) = create_test_state();

    // Verify all components are initialized
    assert!(!state.auth_manager.is_authenticated());

    let auto_refresh = state.auto_refresh_enabled.lock().await;
    assert!(!*auto_refresh);

    let device_flow = state.device_flow_state.read().await;
    assert!(device_flow.is_none());
}

#[tokio::test]
async fn test_toggle_auto_refresh() {
    let (_temp_dir, state) = create_test_state();

    // Initially false
    {
        let enabled = state.auto_refresh_enabled.lock().await;
        assert!(!*enabled);
    }

    // Toggle to true
    {
        let mut enabled = state.auto_refresh_enabled.lock().await;
        *enabled = true;
    }

    // Verify it's true
    {
        let enabled = state.auto_refresh_enabled.lock().await;
        assert!(*enabled);
    }

    // Toggle back to false
    {
        let mut enabled = state.auto_refresh_enabled.lock().await;
        *enabled = false;
    }

    // Verify it's false again
    {
        let enabled = state.auto_refresh_enabled.lock().await;
        assert!(!*enabled);
    }
}

#[tokio::test]
async fn test_device_flow_state_management() {
    let (_temp_dir, state) = create_test_state();

    // Initially none
    {
        let flow_state = state.device_flow_state.read().await;
        assert!(flow_state.is_none());
    }

    // Set device flow state
    {
        let mut flow_state = state.device_flow_state.write().await;
        *flow_state = Some(DeviceFlowState {
            device_code: "device123".to_string(),
            user_code: "ABC123".to_string(),
            verification_uri: "https://github.com/login/device".to_string(),
            interval: 5,
        });
    }

    // Verify state was set
    {
        let flow_state = state.device_flow_state.read().await;
        assert!(flow_state.is_some());
        let flow = flow_state.as_ref().unwrap();
        assert_eq!(flow.device_code, "device123");
        assert_eq!(flow.user_code, "ABC123");
        assert_eq!(flow.verification_uri, "https://github.com/login/device");
        assert_eq!(flow.interval, 5);
    }

    // Clear device flow state (simulate cancel)
    {
        let mut flow_state = state.device_flow_state.write().await;
        *flow_state = None;
    }

    // Verify state was cleared
    {
        let flow_state = state.device_flow_state.read().await;
        assert!(flow_state.is_none());
    }
}

#[tokio::test]
async fn test_is_github_authenticated_command() {
    let (_temp_dir, state) = create_test_state();

    // Should return false initially
    let result = state.auth_manager.is_authenticated();
    assert!(!result);
}

#[tokio::test]
async fn test_get_configured_providers_empty() {
    let (_temp_dir, state) = create_test_state();

    let configs = state.config_loader.load_config().await;
    // Filter to only configs with test-provider prefix (discovered providers are filtered out)
    let test_configs: Vec<_> = configs.iter().filter(|c| c.provider_id.starts_with("test-")).collect();
    assert!(test_configs.is_empty(), "Expected no test configs, found {}", test_configs.len());
}

#[tokio::test]
async fn test_save_and_remove_provider_config() {
    let (_temp_dir, state) = create_test_state();

    // Initially only test-prefixed configs should be empty
    let configs = state.config_loader.load_config().await;
    let initial_test_configs: Vec<_> = configs.iter().filter(|c| c.provider_id.starts_with("test-")).collect();
    assert!(initial_test_configs.is_empty(), "Expected no initial test configs");

    // Add a provider config with unique test ID
    let new_config = aic_core::ProviderConfig {
        provider_id: "test-provider-unique123".to_string(),
        api_key: "test-api-key".to_string(),
        show_in_tray: true,
        ..Default::default()
    };

    let mut configs = state.config_loader.load_config().await;
    configs.push(new_config);
    state.config_loader.save_config(&configs).await.unwrap();

    // Verify it was saved
    let configs = state.config_loader.load_config().await;
    let test_configs: Vec<_> = configs.iter().filter(|c| c.provider_id == "test-provider-unique123").collect();
    assert_eq!(test_configs.len(), 1);
    assert_eq!(test_configs[0].provider_id, "test-provider-unique123");
    assert_eq!(test_configs[0].api_key, "test-api-key");

    // Remove the config
    let mut configs = state.config_loader.load_config().await;
    configs.retain(|c| c.provider_id != "test-provider-unique123");
    state.config_loader.save_config(&configs).await.unwrap();

    // Verify it was removed
    let configs = state.config_loader.load_config().await;
    let test_configs: Vec<_> = configs.iter().filter(|c| c.provider_id == "test-provider-unique123").collect();
    assert!(test_configs.is_empty(), "Test config should be removed");
}

#[tokio::test]
async fn test_save_provider_config_updates_existing() {
    let (_temp_dir, state) = create_test_state();

    // Add initial config with unique ID
    let config1 = aic_core::ProviderConfig {
        provider_id: "test-provider-update456".to_string(),
        api_key: "initial-key".to_string(),
        show_in_tray: true,
        ..Default::default()
    };

    let mut configs = state.config_loader.load_config().await;
    configs.push(config1);
    state.config_loader.save_config(&configs).await.unwrap();

    // Update existing config
    let config2 = aic_core::ProviderConfig {
        provider_id: "test-provider-update456".to_string(),
        api_key: "updated-key".to_string(),
        show_in_tray: false,
        ..Default::default()
    };

    let mut configs = state.config_loader.load_config().await;
    if let Some(existing) = configs
        .iter_mut()
        .find(|c| c.provider_id == "test-provider-update456")
    {
        *existing = config2;
    }
    state.config_loader.save_config(&configs).await.unwrap();

    // Verify it was updated
    let configs = state.config_loader.load_config().await;
    let test_configs: Vec<_> = configs.iter().filter(|c| c.provider_id == "test-provider-update456").collect();
    assert_eq!(test_configs.len(), 1);
    assert_eq!(test_configs[0].api_key, "updated-key");
    assert!(!test_configs[0].show_in_tray);
}

#[tokio::test]
async fn test_load_and_save_preferences() {
    let (_temp_dir, state) = create_test_state();

    // Load default preferences
    let prefs = state.config_loader.load_preferences().await;
    assert!(!prefs.show_all);
    assert!(prefs.always_on_top);
    assert_eq!(prefs.window_width, 420.0);
    assert_eq!(prefs.window_height, 500.0);

    // Modify and save
    let mut new_prefs = prefs.clone();
    new_prefs.show_all = true;
    new_prefs.window_width = 800.0;

    state
        .config_loader
        .save_preferences(&new_prefs)
        .await
        .unwrap();

    // Load again and verify
    let loaded_prefs = state.config_loader.load_preferences().await;
    assert!(loaded_prefs.show_all);
    assert_eq!(loaded_prefs.window_width, 800.0);
}
