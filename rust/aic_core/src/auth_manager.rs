use crate::{
    github_auth::{DeviceFlowResponse, GitHubAuthService, TokenPollResult},
    ConfigLoader, ProviderConfig,
};
use std::sync::Arc;

/// Manages authentication state and coordinates between GitHub auth service and configuration
pub struct AuthenticationManager {
    auth_service: Arc<GitHubAuthService>,
    config_loader: Arc<ConfigLoader>,
}

impl AuthenticationManager {
    /// Create a new authentication manager
    pub fn new(auth_service: Arc<GitHubAuthService>, config_loader: Arc<ConfigLoader>) -> Self {
        Self {
            auth_service,
            config_loader,
        }
    }

    /// Check if currently authenticated with GitHub
    pub fn is_authenticated(&self) -> bool {
        self.auth_service.is_authenticated()
    }

    /// Get the current authentication token if available
    pub fn get_current_token(&self) -> Option<String> {
        self.auth_service.get_current_token()
    }

    /// Initialize the manager with a stored token from configuration
    pub async fn initialize_from_config(&self) {
        let configs = self.config_loader.load_config().await;
        if let Some(copilot_config) = configs.iter().find(|c| c.provider_id == "github-copilot") {
            if !copilot_config.api_key.is_empty() {
                self.auth_service
                    .initialize_token(copilot_config.api_key.clone());
            }
        }
    }

    /// Initiate the GitHub device flow login
    pub async fn initiate_login(&self) -> Result<DeviceFlowResponse, String> {
        self.auth_service
            .initiate_device_flow()
            .await
            .map_err(|e| e.to_string())
    }

    /// Wait for login completion with automatic polling
    pub async fn wait_for_login(&self, device_code: &str, interval: u64) -> Result<bool, String> {
        match self
            .auth_service
            .complete_device_flow(device_code, interval, None)
            .await
        {
            Ok(token) => {
                self.save_token(&token).await?;
                Ok(true)
            }
            Err(e) => {
                log::error!("Failed to complete device flow: {}", e);
                Ok(false)
            }
        }
    }

    /// Poll once for token (for manual polling implementations)
    pub async fn poll_for_token(&self, device_code: &str) -> TokenPollResult {
        let result = self.auth_service.poll_for_token(device_code).await;

        // If we got a token, save it
        if let TokenPollResult::Token(ref token) = result {
            if let Err(e) = self.save_token(token).await {
                log::error!("Failed to save token: {}", e);
            }
        }

        result
    }

    /// Logout and clear the stored token
    pub async fn logout(&self) -> Result<(), String> {
        self.auth_service.logout();

        let mut configs = self.config_loader.load_config().await;
        if let Some(copilot_config) = configs
            .iter_mut()
            .find(|c| c.provider_id == "github-copilot")
        {
            copilot_config.api_key.clear();
            self.config_loader
                .save_config(&configs)
                .await
                .map_err(|e| e.to_string())?;
        }

        Ok(())
    }

    /// Save token to configuration
    async fn save_token(&self, token: &str) -> Result<(), String> {
        let mut configs = self.config_loader.load_config().await;

        if let Some(c) = configs
            .iter_mut()
            .find(|c| c.provider_id == "github-copilot")
        {
            c.api_key = token.to_string();
        } else {
            let new_config = ProviderConfig {
                provider_id: "github-copilot".to_string(),
                api_key: token.to_string(),
                show_in_tray: true,
                ..Default::default()
            };
            configs.push(new_config);
        }

        self.config_loader
            .save_config(&configs)
            .await
            .map_err(|e| e.to_string())?;
        log::info!("GitHub Copilot token saved to configuration");

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use reqwest::Client;

    #[tokio::test]
    async fn test_authentication_manager_new() {
        let client = Client::new();
        let auth_service = Arc::new(GitHubAuthService::new(client.clone()));
        let config_loader = Arc::new(ConfigLoader::new(client));

        let manager = AuthenticationManager::new(auth_service, config_loader);

        assert!(!manager.is_authenticated());
        assert!(manager.get_current_token().is_none());
    }

    #[test]
    fn test_is_authenticated_initially_false() {
        let client = Client::new();
        let auth_service = Arc::new(GitHubAuthService::new(client.clone()));
        let config_loader = Arc::new(ConfigLoader::new(client));

        let manager = AuthenticationManager::new(auth_service, config_loader);

        assert!(!manager.is_authenticated());
    }
}
