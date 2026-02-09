// Prevents additional console window on Windows in release
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use aic_core::AppPreferences;
use reqwest::Client;
use serde::{Deserialize, Serialize};
use std::process::Command;
use std::sync::Arc;
use std::time::Duration;
use tauri::menu::MenuEvent;
use tauri::{
    menu::{Menu, MenuItem},
    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},
    Emitter, Manager, Runtime, State, WebviewWindowBuilder,
};
use tokio::sync::{Mutex, RwLock};
use tokio::time::interval;

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct AgentConfig {
    pub listen_address: String,
    pub use_agent: bool,
    pub auto_connect: bool,
    pub connection_timeout_seconds: u64,
}

impl Default for AgentConfig {
    fn default() -> Self {
        Self {
            listen_address: "http://127.0.0.1:8080".to_string(),
            use_agent: true,
            auto_connect: false,
            connection_timeout_seconds: 5,
        }
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct AgentStatus {
    pub running: bool,
    pub polling_enabled: bool,
    pub polling_interval_seconds: u64,
    pub last_refresh: Option<String>,
    pub next_refresh: Option<String>,
    pub database_path: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UsageSummary {
    pub total_providers: usize,
    pub active_providers: usize,
    pub providers: Vec<ProviderSummary>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ProviderSummary {
    pub provider_id: String,
    pub provider_name: String,
    pub is_available: bool,
    pub usage_percentage: f64,
    pub cost_used: f64,
    pub cost_limit: f64,
    pub last_updated: Option<String>,
}

struct AppState {
    http_client: Arc<Client>,
    agent_config: Arc<RwLock<AgentConfig>>,
    auto_refresh_enabled: Arc<Mutex<bool>>,
    device_flow_state: Arc<RwLock<Option<DeviceFlowState>>>,
}

#[derive(Clone)]
struct DeviceFlowState {
    device_code: String,
    user_code: String,
    verification_uri: String,
    interval: u64,
}

impl AppState {
    async fn agent_url(&self) -> String {
        let config = self.agent_config.read().await;
        config.listen_address.clone()
    }

    async fn check_agent_available(&self) -> bool {
        let url = format!("{}/health", self.agent_url().await);
        match self.http_client.get(&url).send().await {
            Ok(res) => res.status().is_success(),
            Err(_) => false,
        }
    }

    async fn get_usage_from_agent(&self) -> Result<UsageSummary, String> {
        let url = format!("{}/api/v1/usage", self.agent_url().await);
        match self.http_client.get(&url).send().await {
            Ok(res) => {
                if res.status().is_success() {
                    let json: serde_json::Value = res.json().await.map_err(|e| e.to_string())?;
                    let data = json.get("data").ok_or("No data in response")?;
                    serde_json::from_value(data.clone()).map_err(|e| e.to_string())
                } else {
                    Err(format!("Agent returned status: {}", res.status()))
                }
            }
            Err(e) => Err(format!("Failed to connect to agent: {}", e)),
        }
    }

    async fn refresh_agent(&self) -> Result<usize, String> {
        let url = format!("{}/api/v1/refresh", self.agent_url().await);
        match self.http_client.post(&url).send().await {
            Ok(res) => {
                if res.status().is_success() {
                    let json: serde_json::Value = res.json().await.map_err(|e| e.to_string())?;
                    let data = json.get("data").ok_or("No data in response")?;
                    let count = data.as_i64().ok_or("Invalid count")?;
                    Ok(count as usize)
                } else {
                    Err(format!("Agent returned status: {}", res.status()))
                }
            }
            Err(e) => Err(format!("Failed to connect to agent: {}", e)),
        }
    }

    async fn get_agent_status(&self) -> Result<AgentStatus, String> {
        let url = format!("{}/api/v1/status", self.agent_url().await);
        match self.http_client.get(&url).send().await {
            Ok(res) => {
                if res.status().is_success() {
                    let json: serde_json::Value = res.json().await.map_err(|e| e.to_string())?;
                    let data = json.get("data").ok_or("No data in response")?;
                    serde_json::from_value(data.clone()).map_err(|e| e.to_string())
                } else {
                    Err(format!("Agent returned status: {}", res.status()))
                }
            }
            Err(e) => Err(format!("Failed to connect to agent: {}", e)),
        }
    }
}

// Agent commands
#[tauri::command]
async fn get_usage(state: State<'_, AppState>) -> Result<UsageSummary, String> {
    let use_agent = {
        let config = state.agent_config.read().await;
        config.use_agent
    };

    if !use_agent || !state.check_agent_available().await {
        // Fallback to mock data when agent is not available
        Ok(UsageSummary {
            total_providers: 0,
            active_providers: 0,
            providers: vec![],
        })
    } else {
        state.get_usage_from_agent().await
    }
}

#[tauri::command]
async fn refresh_usage(state: State<'_, AppState>) -> Result<UsageSummary, String> {
    let use_agent = {
        let config = state.agent_config.read().await;
        config.use_agent
    };

    if !use_agent || !state.check_agent_available().await {
        // Trigger refresh via agent if available
        let _ = state.refresh_agent().await;
        // Return current usage
        state.get_usage_from_agent().await
    } else {
        state.refresh_agent().await?;
        state.get_usage_from_agent().await
    }
}

#[tauri::command]
async fn get_agent_status(state: State<'_, AppState>) -> Result<AgentStatus, String> {
    state.get_agent_status().await
}

#[tauri::command]
async fn is_agent_running(state: State<'_, AppState>) -> Result<bool, String> {
    Ok(state.check_agent_available().await)
}

#[tauri::command]
async fn check_agent_connection(state: State<'_, AppState>) -> Result<bool, String> {
    Ok(state.check_agent_available().await)
}

// Preferences commands
#[tauri::command]
async fn load_preferences() -> Result<AppPreferences, String> {
    let client = reqwest::Client::new();
    let loader = aic_core::ConfigLoader::new(client);
    Ok(loader.load_preferences().await)
}

#[tauri::command]
async fn save_preferences(preferences: AppPreferences) -> Result<(), String> {
    let client = reqwest::Client::new();
    let loader = aic_core::ConfigLoader::new(client);
    loader.save_preferences(&preferences).await.map_err(|e| e.to_string())
}

// Config commands
#[tauri::command]
async fn get_configured_providers() -> Result<Vec<aic_core::ProviderConfig>, String> {
    let client = reqwest::Client::new();
    let loader = aic_core::ConfigLoader::new(client);
    Ok(loader.load_config().await)
}

#[tauri::command]
async fn save_provider_config(config: aic_core::ProviderConfig) -> Result<(), String> {
    let client = reqwest::Client::new();
    let loader = aic_core::ConfigLoader::new(client);
    let mut configs = loader.load_config().await;

    if let Some(existing) = configs.iter_mut().find(|c| c.provider_id == config.provider_id) {
        *existing = config;
    } else {
        configs.push(config);
    }

    loader.save_config(&configs).await.map_err(|e| e.to_string())
}

#[tauri::command]
async fn remove_provider_config(provider_id: String) -> Result<(), String> {
    let client = reqwest::Client::new();
    let loader = aic_core::ConfigLoader::new(client);
    let mut configs = loader.load_config().await;
    configs.retain(|c| c.provider_id != provider_id);
    loader.save_config(&configs).await.map_err(|e| e.to_string())
}

// Agent configuration commands
#[tauri::command]
async fn load_agent_config(state: State<'_, AppState>) -> Result<AgentConfig, String> {
    let config = state.agent_config.read().await;
    Ok(config.clone())
}

#[tauri::command]
async fn save_agent_config(state: State<'_, AppState>, config: AgentConfig) -> Result<(), String> {
    let mut config_guard = state.agent_config.write().await;
    *config_guard = config;
    Ok(())
}

#[tauri::command]
async fn start_agent(_app: tauri::AppHandle) -> Result<(), String> {
    // On Windows, try to start the agent service
    #[cfg(target_os = "windows")]
    {
        let _ = Command::new("powershell")
            .args(&["-Command", "Start-Service", "aic-agent"])
            .spawn();
    }

    // Try to start the agent binary
    let agent_path = std::env::current_exe()
        .ok()
        .map(|p| p.parent().unwrap().join("aic-agent.exe"));

    if let Some(path) = agent_path {
        if path.exists() {
            let _ = Command::new(&path).args(&["start"]).spawn();
            return Ok(());
        }
    }

    // Fallback: just return success, user needs to start agent manually
    Ok(())
}

#[tauri::command]
async fn stop_agent() -> Result<(), String> {
    // Try to stop via API first
    let client = reqwest::Client::new();
    let url = "http://127.0.0.1:8080/api/v1/shutdown";

    if let Ok(_) = client.post(url).send().await {
        return Ok(());
    }

    // On Windows, try to stop the service
    #[cfg(target_os = "windows")]
    {
        let _ = Command::new("powershell")
            .args(&["-Command", "Stop-Service", "aic-agent", "-ErrorAction", "SilentlyContinue"])
            .spawn();
    }

    Ok(())
}

// Auto-refresh commands
#[tauri::command]
async fn toggle_auto_refresh(state: State<'_, AppState>, enabled: bool) -> Result<(), String> {
    let mut auto_refresh = state.auto_refresh_enabled.lock().await;
    *auto_refresh = enabled;
    Ok(())
}

#[tauri::command]
async fn is_auto_refresh_enabled(state: State<'_, AppState>) -> Result<bool, String> {
    let auto_refresh = state.auto_refresh_enabled.lock().await;
    Ok(*auto_refresh)
}

// GitHub Authentication commands
#[tauri::command]
async fn is_github_authenticated() -> Result<bool, String> {
    let client = reqwest::Client::new();
    let auth_service = Arc::new(aic_core::GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(aic_core::ConfigLoader::new(client));
    let auth_manager = Arc::new(aic_core::AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));
    auth_manager.initialize_from_config().await;
    Ok(auth_manager.is_authenticated())
}

#[tauri::command]
async fn initiate_github_login() -> Result<(String, String, String), String> {
    let client = reqwest::Client::new();
    let auth_service = Arc::new(aic_core::GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(aic_core::ConfigLoader::new(client));
    let auth_manager = Arc::new(aic_core::AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    match auth_manager.initiate_login().await {
        Ok(flow_response) => Ok((
            flow_response.user_code,
            flow_response.verification_uri,
            flow_response.device_code,
        )),
        Err(e) => Err(format!("Failed to initiate login: {}", e)),
    }
}

#[tauri::command]
async fn complete_github_login(
    device_code: String,
    interval: u64,
) -> Result<bool, String> {
    let client = reqwest::Client::new();
    let auth_service = Arc::new(aic_core::GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(aic_core::ConfigLoader::new(client));
    let auth_manager = Arc::new(aic_core::AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    match auth_manager.wait_for_login(&device_code, interval).await {
        Ok(success) => Ok(success),
        Err(e) => Err(format!("Login failed: {}", e)),
    }
}

#[tauri::command]
async fn poll_github_token(device_code: String) -> Result<String, String> {
    use aic_core::TokenPollResult;

    let client = reqwest::Client::new();
    let auth_service = Arc::new(aic_core::GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(aic_core::ConfigLoader::new(client));
    let auth_manager = Arc::new(aic_core::AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    match auth_manager.poll_for_token(&device_code).await {
        TokenPollResult::Token(_) => Ok("success".to_string()),
        TokenPollResult::Pending => Ok("pending".to_string()),
        TokenPollResult::SlowDown => Ok("slow_down".to_string()),
        TokenPollResult::Expired => Err("Token expired".to_string()),
        TokenPollResult::AccessDenied => Err("Access denied".to_string()),
        TokenPollResult::Error(msg) => Err(msg),
    }
}

#[tauri::command]
async fn logout_github() -> Result<(), String> {
    let client = reqwest::Client::new();
    let auth_service = Arc::new(aic_core::GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(aic_core::ConfigLoader::new(client));
    let auth_manager = Arc::new(aic_core::AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    auth_manager.logout().await.map_err(|e| format!("Logout failed: {}", e))
}

// Window control commands
#[tauri::command]
async fn close_window(window: tauri::Window) -> Result<(), String> {
    let _ = window.close();
    Ok(())
}

#[tauri::command]
async fn minimize_window(window: tauri::Window) -> Result<(), String> {
    let _ = window.minimize();
    Ok(())
}

#[tauri::command]
async fn toggle_always_on_top(window: tauri::Window, enabled: bool) -> Result<(), String> {
    let _ = window.set_always_on_top(enabled);
    Ok(())
}

#[tauri::command]
async fn open_browser(url: String) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    {
        let _ = Command::new("cmd").args(["/C", "start", &url]).spawn();
    }
    #[cfg(target_os = "macos")]
    {
        let _ = Command::new("open").arg(&url).spawn();
    }
    #[cfg(target_os = "linux")]
    {
        let _ = Command::new("xdg-open").arg(&url).spawn();
    }
    Ok(())
}

#[tauri::command]
async fn close_settings_window(window: tauri::Window) -> Result<(), String> {
    let _ = window.close();
    Ok(())
}

#[tauri::command]
async fn open_settings_window(app: tauri::AppHandle) -> Result<(), String> {
    println!("Opening settings window...");

    if let Some(window) = app.get_webview_window("settings") {
        let _ = window.show();
        let _ = window.set_focus();
        return Ok(());
    }

    let _ = WebviewWindowBuilder::new(
        &app,
        "settings",
        tauri::WebviewUrl::App("settings.html".into()),
    )
    .title("Settings")
    .inner_size(500.0, 550.0)
    .min_inner_size(400.0, 400.0)
    .center()
    .decorations(false)
    .transparent(true)
    .build()
    .map_err(|e| e.to_string())?;

    Ok(())
}

fn create_tray_menu<R: Runtime>(
    app: &tauri::AppHandle<R>,
) -> Result<Menu<R>, Box<dyn std::error::Error>> {
    let show_i = MenuItem::with_id(app, "show", "Show", true, None::<&str>)?;
    let refresh_i = MenuItem::with_id(app, "refresh", "Refresh", true, None::<&str>)?;
    let auto_refresh_i =
        MenuItem::with_id(app, "auto_refresh", "Auto Refresh", true, None::<&str>)?;
    let settings_i = MenuItem::with_id(app, "settings", "Settings", true, None::<&str>)?;
    let quit_i = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;

    let menu = Menu::with_items(
        app,
        &[
            &show_i,
            &refresh_i,
            &auto_refresh_i,
            &MenuItem::with_id(app, "separator1", "---", false, None::<&str>)?,
            &settings_i,
            &MenuItem::with_id(app, "separator2", "---", false, None::<&str>)?,
            &quit_i,
        ],
    )?;

    Ok(menu)
}

#[tokio::main]
async fn main() {
    let http_client = Arc::new(reqwest::Client::new());

    let agent_config = Arc::new(RwLock::new(AgentConfig::default()));

    let auto_refresh_enabled = Arc::new(Mutex::new(false));

    tauri::Builder::default()
        .manage(AppState {
            http_client,
            agent_config,
            auto_refresh_enabled,
            device_flow_state: Arc::new(RwLock::new(None)),
        })
        .plugin(tauri_plugin_shell::init())
        .invoke_handler(tauri::generate_handler![
            // Agent commands
            get_usage,
            refresh_usage,
            get_agent_status,
            is_agent_running,
            check_agent_connection,
            load_agent_config,
            save_agent_config,
            start_agent,
            stop_agent,
            // Preferences commands
            load_preferences,
            save_preferences,
            // Config commands
            get_configured_providers,
            save_provider_config,
            remove_provider_config,
            // Auto-refresh commands
            toggle_auto_refresh,
            is_auto_refresh_enabled,
            // GitHub Authentication commands
            is_github_authenticated,
            initiate_github_login,
            complete_github_login,
            poll_github_token,
            logout_github,
            // Window control commands
            close_window,
            minimize_window,
            toggle_always_on_top,
            // Browser command
            open_browser,
            // Settings commands
            close_settings_window,
            open_settings_window,
        ])
        .setup(|app| {
            let menu = create_tray_menu(app.handle())?;

            let _tray = TrayIconBuilder::new()
                .icon(app.default_window_icon().unwrap().clone())
                .menu(&menu)
                .show_menu_on_left_click(true)
                .on_menu_event(|app: &tauri::AppHandle, event: MenuEvent| {
                    match event.id().as_ref() {
                        "show" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _ = window.show();
                                let _ = window.set_focus();
                            }
                        }
                        "refresh" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _ = window.emit("refresh-requested", ());
                            }
                        }
                        "auto_refresh" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _ = window.emit("toggle-auto-refresh", ());
                            }
                        }
                        "settings" => {
                            if app.get_webview_window("settings").is_none() {
                                let _ = WebviewWindowBuilder::new(
                                    app,
                                    "settings",
                                    tauri::WebviewUrl::App("settings.html".into()),
                                )
                                .title("Settings")
                                .inner_size(500.0, 550.0)
                                .min_inner_size(400.0, 400.0)
                                .center()
                                .decorations(false)
                                .transparent(true)
                                .build();
                            } else if let Some(window) = app.get_webview_window("settings") {
                                let _ = window.show();
                                let _ = window.set_focus();
                            }
                        }
                        "quit" => {
                            app.exit(0);
                        }
                        _ => {}
                    }
                })
                .on_tray_icon_event(|tray: &tauri::tray::TrayIcon, event: TrayIconEvent| {
                    if let TrayIconEvent::Click {
                        button: MouseButton::Left,
                        button_state: MouseButtonState::Up,
                        ..
                    } = event
                    {
                        let app = tray.app_handle();
                        if let Some(window) = app.get_webview_window("main") {
                            let _ = window.show();
                            let _ = window.set_focus();
                        }
                    }
                })
                .build(app)?;

            if let Some(window) = app.get_webview_window("main") {
                window.show()?;
                window.set_focus()?;
                println!("Main window shown successfully");
            } else {
                println!("WARNING: Main window not found!");
            }

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
