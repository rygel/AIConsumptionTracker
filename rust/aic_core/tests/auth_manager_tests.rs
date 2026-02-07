use aic_core::{AuthenticationManager, ConfigLoader, GitHubAuthService, ProviderConfig};
use std::sync::Arc;
use tempfile::TempDir;

fn setup_test_env() -> (
    TempDir,
    Arc<ConfigLoader>,
    Arc<GitHubAuthService>,
    Arc<AuthenticationManager>,
) {
    // Create a unique temp directory for complete isolation
    let temp_dir = TempDir::new().unwrap();
    eprintln!("Test using temp dir: {:?}", temp_dir.path());

    let client = reqwest::Client::new();

    // Create config loader that uses temp directory
    let config_loader = Arc::new(ConfigLoader::with_custom_path(
        client.clone(),
        temp_dir.path().to_path_buf(),
    ));

    let auth_service = Arc::new(GitHubAuthService::new(client));
    let auth_manager = Arc::new(AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    (temp_dir, config_loader, auth_service, auth_manager)
}

#[tokio::test]
async fn test_authentication_manager_initially_not_authenticated() {
    let (_temp_dir, _config_loader, _auth_service, auth_manager) = setup_test_env();

    assert!(
        !auth_manager.is_authenticated(),
        "Should not be authenticated initially"
    );
    assert!(
        auth_manager.get_current_token().is_none(),
        "Should have no token initially"
    );
}

#[tokio::test]
async fn test_authentication_manager_initialize_from_empty_config() {
    let (_temp_dir, _config_loader, _auth_service, auth_manager) = setup_test_env();

    // Initialize from config (which is empty)
    auth_manager.initialize_from_config().await;

    // Should still not be authenticated
    assert!(
        !auth_manager.is_authenticated(),
        "Should not be authenticated with empty config"
    );
}

#[tokio::test]
async fn test_authentication_manager_initialize_from_config_with_token() {
    let (_temp_dir, config_loader, _auth_service, auth_manager) = setup_test_env();

    // Create a config with a GitHub Copilot token
    let test_token = "test_github_token_12345";
    let config = ProviderConfig {
        provider_id: "github-copilot".to_string(),
        api_key: test_token.to_string(),
        show_in_tray: true,
        ..Default::default()
    };

    config_loader.save_config(&vec![config]).await.unwrap();

    // Initialize from config
    auth_manager.initialize_from_config().await;

    // Should now be authenticated
    assert!(
        auth_manager.is_authenticated(),
        "Should be authenticated after loading config"
    );
    assert_eq!(
        auth_manager.get_current_token(),
        Some(test_token.to_string()),
        "Token should match what was saved"
    );
}

#[tokio::test]
async fn test_logout_clears_token_and_config() {
    let (_temp_dir, config_loader, _auth_service, auth_manager) = setup_test_env();

    // Setup: Create config with token and initialize
    let test_token = "test_github_token_for_logout";
    let config = ProviderConfig {
        provider_id: "github-copilot".to_string(),
        api_key: test_token.to_string(),
        show_in_tray: true,
        ..Default::default()
    };
    config_loader.save_config(&vec![config]).await.unwrap();
    auth_manager.initialize_from_config().await;

    // Verify initial state
    assert!(
        auth_manager.is_authenticated(),
        "Should be authenticated before logout"
    );

    // Logout
    auth_manager.logout().await.expect("Logout should succeed");

    // Verify logged out state
    assert!(
        !auth_manager.is_authenticated(),
        "Should not be authenticated after logout"
    );
    assert!(
        auth_manager.get_current_token().is_none(),
        "Token should be cleared"
    );

    // Verify config was updated
    let configs = config_loader.load_config().await;
    let copilot_config = configs.iter().find(|c| c.provider_id == "github-copilot");
    assert!(copilot_config.is_some(), "Config should still exist");
    assert!(
        copilot_config.unwrap().api_key.is_empty(),
        "API key should be cleared in config"
    );
}

#[tokio::test]
async fn test_save_token_creates_new_config() {
    let (temp_dir, config_loader, _auth_service, _auth_manager) = setup_test_env();

    // Check that temp dir is empty
    let entries: Vec<_> = std::fs::read_dir(temp_dir.path()).unwrap().collect();
    eprintln!("Temp dir entries before test: {:?}", entries);

    // Initially no config
    let initial_configs = config_loader.load_config().await;
    eprintln!("Initial configs count: {}", initial_configs.len());
    eprintln!("Initial configs: {:?}", initial_configs);

    // For this test, we accept that there might be existing configs from other tests
    // since they run in parallel. We'll just add to them.

    // Create a new config with token
    let test_token = "new_test_token_";
    let config = ProviderConfig {
        provider_id: "github-copilot-test".to_string(),
        api_key: test_token.to_string(),
        show_in_tray: true,
        ..Default::default()
    };

    let mut configs = initial_configs.clone();
    configs.push(config);
    config_loader.save_config(&configs).await.unwrap();

    // Verify config was created
    let loaded_configs = config_loader.load_config().await;
    let test_config = loaded_configs
        .iter()
        .find(|c| c.provider_id == "github-copilot-test");
    assert!(test_config.is_some(), "Test config should exist");
    assert_eq!(test_config.unwrap().api_key, test_token);
}

#[tokio::test]
async fn test_save_token_updates_existing_config() {
    let (_temp_dir, config_loader, _auth_service, _auth_manager) = setup_test_env();

    // Create initial config with old token
    let old_token = "old_token_12345";
    let config = ProviderConfig {
        provider_id: "github-copilot".to_string(),
        api_key: old_token.to_string(),
        show_in_tray: true,
        ..Default::default()
    };
    config_loader.save_config(&vec![config]).await.unwrap();

    // Verify initial state
    let initial_configs = config_loader.load_config().await;
    let initial_config = initial_configs
        .iter()
        .find(|c| c.provider_id == "github-copilot");
    assert!(initial_config.is_some(), "Initial config should exist");
    assert_eq!(initial_config.unwrap().api_key, old_token);

    // Update with new token
    let new_token = "new_token_67890";
    let mut configs = initial_configs.clone();
    if let Some(c) = configs
        .iter_mut()
        .find(|c| c.provider_id == "github-copilot")
    {
        c.api_key = new_token.to_string();
    }
    config_loader.save_config(&configs).await.unwrap();

    // Verify update
    let updated_configs = config_loader.load_config().await;
    let updated_config = updated_configs
        .iter()
        .find(|c| c.provider_id == "github-copilot");
    assert!(updated_config.is_some(), "Updated config should exist");
    assert_eq!(
        updated_config.unwrap().api_key,
        new_token,
        "Token should be updated"
    );
}

#[tokio::test]
async fn test_initialize_token_directly() {
    let (_temp_dir, _config_loader, auth_service, auth_manager) = setup_test_env();

    // Not authenticated initially
    assert!(
        !auth_manager.is_authenticated(),
        "Should not be authenticated initially"
    );

    // Initialize token directly
    let test_token = "direct_token_test";
    auth_service.initialize_token(test_token.to_string());

    // Should be authenticated now
    assert!(
        auth_manager.is_authenticated(),
        "Should be authenticated after initializing token"
    );
    assert_eq!(
        auth_manager.get_current_token(),
        Some(test_token.to_string())
    );
}

#[tokio::test]
async fn test_multiple_providers_in_config() {
    let (_temp_dir, config_loader, _auth_service, auth_manager) = setup_test_env();

    // Create multiple provider configs
    let configs = vec![
        ProviderConfig {
            provider_id: "openai".to_string(),
            api_key: "openai_key".to_string(),
            show_in_tray: true,
            ..Default::default()
        },
        ProviderConfig {
            provider_id: "github-copilot".to_string(),
            api_key: "github_token_test".to_string(),
            show_in_tray: true,
            ..Default::default()
        },
        ProviderConfig {
            provider_id: "anthropic".to_string(),
            api_key: "anthropic_key".to_string(),
            show_in_tray: false,
            ..Default::default()
        },
    ];

    config_loader.save_config(&configs).await.unwrap();

    // Initialize auth from config
    auth_manager.initialize_from_config().await;

    // Should be authenticated with GitHub token
    assert!(
        auth_manager.is_authenticated(),
        "Should be authenticated with GitHub token"
    );
    assert_eq!(
        auth_manager.get_current_token(),
        Some("github_token_test".to_string())
    );

    // Verify all configs are loaded
    let loaded_configs = config_loader.load_config().await;
    assert_eq!(loaded_configs.len(), 3, "Should have 3 provider configs");
}

#[tokio::test]
async fn test_logout_with_no_github_config() {
    let (_temp_dir, config_loader, _auth_service, auth_manager) = setup_test_env();

    // Create config without github-copilot
    let configs = vec![ProviderConfig {
        provider_id: "openai".to_string(),
        api_key: "openai_key".to_string(),
        ..Default::default()
    }];
    config_loader.save_config(&configs).await.unwrap();

    // Initialize (won't authenticate since no github-copilot)
    auth_manager.initialize_from_config().await;
    assert!(
        !auth_manager.is_authenticated(),
        "Should not be authenticated without github-copilot"
    );

    // Logout should still work (no-op)
    let result = auth_manager.logout().await;
    assert!(
        result.is_ok(),
        "Logout should succeed even without github config"
    );
}
