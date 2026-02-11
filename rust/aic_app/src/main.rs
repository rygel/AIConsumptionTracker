// Prevents additional console window on Windows in release
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use aic_core::{
    AuthenticationManager, ConfigLoader, GitHubAuthService, ProviderManager, ProviderUsage,
};
use aic_app::commands::*;
use std::process::{Command, Child};
use std::sync::Arc;
use std::time::Duration;

use tauri::{
    menu::{Menu, MenuItem},
    tray::TrayIconBuilder,
    Emitter, Manager, Runtime, State, WebviewWindowBuilder, AppHandle,
};
use tauri_plugin_updater::UpdaterExt;
use tokio::sync::{Mutex, RwLock};
use tokio::time::interval;

struct AppState {
    provider_manager: Arc<ProviderManager>,
    config_loader: Arc<ConfigLoader>,
    auth_manager: Arc<AuthenticationManager>,
    auto_refresh_enabled: Arc<Mutex<bool>>,
    device_flow_state: Arc<RwLock<Option<DeviceFlowState>>>,
    agent_process: Arc<Mutex<Option<Child>>>,
}

#[derive(Clone)]
struct DeviceFlowState {
    device_code: String,
    user_code: String,
    verification_uri: String,
    interval: u64,
}

// Provider commands
#[tauri::command]
async fn get_usage(state: State<'_, AppState>) -> Result<Vec<ProviderUsage>, String> {
    let manager = &state.provider_manager;
    Ok(manager.get_all_usage(true).await)
}

#[tauri::command]
async fn refresh_usage(state: State<'_, AppState>) -> Result<Vec<ProviderUsage>, String> {
    let manager = &state.provider_manager;
    Ok(manager.get_all_usage(true).await)
}

// Preferences commands
#[tauri::command]
async fn load_preferences(state: State<'_, AppState>) -> Result<aic_core::AppPreferences, String> {
    let prefs = state.config_loader.load_preferences().await;
    Ok(prefs)
}

#[tauri::command]
async fn save_preferences(
    state: State<'_, AppState>,
    preferences: aic_core::AppPreferences,
) -> Result<(), String> {
    state
        .config_loader
        .save_preferences(&preferences)
        .await
        .map_err(|e| e.to_string())
}

// Config commands
#[tauri::command]
async fn get_configured_providers(
    state: State<'_, AppState>,
) -> Result<Vec<aic_core::ProviderConfig>, String> {
    let configs = state.config_loader.load_config().await;
    Ok(configs)
}

#[tauri::command]
async fn save_provider_config(
    state: State<'_, AppState>,
    config: aic_core::ProviderConfig,
) -> Result<(), String> {
    let mut configs = state.config_loader.load_config().await;

    // Update or add the config
    if let Some(existing) = configs
        .iter_mut()
        .find(|c| c.provider_id == config.provider_id)
    {
        *existing = config;
    } else {
        configs.push(config);
    }

    state
        .config_loader
        .save_config(&configs)
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
async fn remove_provider_config(
    state: State<'_, AppState>,
    provider_id: String,
) -> Result<(), String> {
    let mut configs = state.config_loader.load_config().await;
    configs.retain(|c| c.provider_id != provider_id);

    state
        .config_loader
        .save_config(&configs)
        .await
        .map_err(|e| e.to_string())
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
async fn is_github_authenticated(state: State<'_, AppState>) -> Result<bool, String> {
    Ok(state.auth_manager.is_authenticated())
}

#[tauri::command]
async fn initiate_github_login(
    state: State<'_, AppState>,
) -> Result<(String, String, String), String> {
    match state.auth_manager.initiate_login().await {
        Ok(flow_response) => {
            // Store the device flow state
            let mut flow_state = state.device_flow_state.write().await;
            *flow_state = Some(DeviceFlowState {
                device_code: flow_response.device_code.clone(),
                user_code: flow_response.user_code.clone(),
                verification_uri: flow_response.verification_uri.clone(),
                interval: flow_response.interval as u64,
            });

            Ok((
                flow_response.user_code,
                flow_response.verification_uri,
                flow_response.device_code,
            ))
        }
        Err(e) => Err(format!("Failed to initiate login: {}", e)),
    }
}

#[tauri::command]
async fn get_agent_status_details(state: State<'_, AppState>) -> Result<AgentStatusDetails, String> {
    let mut agent_process = state.agent_process.lock().await;

    if let Some(ref mut child) = *agent_process {
        match child.try_wait() {
            Ok(None) => {
                // Process is still running
                let pid = child.id();
                let path = match &child {
                    Child { .. } => "running".to_string(),
                };
                Ok(AgentStatusDetails {
                    is_running: true,
                    process_id: Some(pid),
                    path_from: "Manual start".to_string(),
                })
            }
            Ok(_) => {
                // Process has finished
                *agent_process = None;
                Ok(AgentStatusDetails {
                    is_running: false,
                    process_id: None,
                    path_from: "Stopped".to_string(),
                })
            }
            Err(_) => {
                // Error occurred, assume process is done
                *agent_process = None;
                Ok(AgentStatusDetails {
                    is_running: false,
                    process_id: None,
                    path_from: "Unknown".to_string(),
                })
            }
        }
    } else {
        Ok(AgentStatusDetails {
            is_running: false,
            process_id: None,
            path_from: "Not started".to_string(),
        })
    }
}

#[derive(serde::Serialize)]
struct AgentStatusDetails {
    is_running: bool,
    process_id: Option<u32>,
    path_from: String,
}

#[tauri::command]
async fn open_settings_window(app: tauri::AppHandle) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("settings") {
        let _ = window.show();
        let _ = window.set_focus();
        Ok(())
    } else {
        Err("Settings window not found".to_string())
    }
}

#[tauri::command]
async fn start_agent(
    app: tauri::AppHandle,
    state: State<'_, AppState>,
) -> Result<bool, String> {
    let agent_process = state.agent_process.clone();
    start_agent_internal(&app, agent_process).await
}

#[tauri::command]
async fn stop_agent(state: State<'_, AppState>) -> Result<bool, String> {
    let mut agent_process = state.agent_process.lock().await;

    if let Some(ref mut child) = *agent_process {
        match child.kill() {
            Ok(_) => {
                log::info!("Agent stopped successfully");
                *agent_process = None;
                Ok(true)
            }
            Err(e) => {
                log::error!("Failed to stop agent: {}", e);
                Err(format!("Failed to stop agent: {}", e))
            }
        }
    } else {
        Err("No agent process running".to_string())
    }
}

#[tauri::command]
async fn is_agent_running(state: State<'_, AppState>) -> Result<bool, String> {
    let mut agent_process = state.agent_process.lock().await;

    if let Some(ref mut child) = *agent_process {
        match child.try_wait() {
            Ok(None) => {
                Ok(true)
            }
            Ok(_) => {
                *agent_process = None;
                Ok(false)
            }
            Err(_) => {
                *agent_process = None;
                Ok(false)
            }
        }
    } else {
        Ok(false)
    }
}

async fn update_tray_icon_by_status(app_handle: &AppHandle, is_connected: bool) {
    let tooltip = if is_connected {
        "AI Token Tracker - Connected to Agent"
    } else {
        "AI Token Tracker - Agent Disconnected"
    };

    if let Some(tray) = app_handle.tray_by_id("main") {
        let _ = tray.set_tooltip(Some(tooltip));
    }
}

async fn check_and_update_tray_status(app_handle: &AppHandle) {
    let state = app_handle.state::<AppState>();
    let mut agent_process = state.agent_process.lock().await;

    let is_connected = if let Some(ref mut child) = *agent_process {
        match child.try_wait() {
            Ok(None) => true,
            Ok(_) => {
                *agent_process = None;
                false
            }
            Err(_) => {
                *agent_process = None;
                false
            }
        }
    } else {
        false
    };

    update_tray_icon_by_status(app_handle, is_connected).await;
}

async fn check_agent_status() -> Result<bool, String> {
    if let Ok(response) = reqwest::get("http://localhost:8080/health").await {
        Ok(response.status().is_success())
    } else {
        Ok(false)
    }
}

async fn start_agent_internal(
    app_handle: &tauri::AppHandle,
    agent_process: Arc<Mutex<Option<Child>>>,
) -> Result<bool, String> {
    let mut agent_process = agent_process.lock().await;

    if let Some(ref mut child) = *agent_process {
        match child.try_wait() {
            Ok(None) => {
                let app_handle = app_handle.clone();
                tokio::spawn(async move {
                    update_tray_icon_by_status(&app_handle, true).await;
                });
                return Ok(true);
            }
            Ok(_) => {
                *agent_process = None;
            }
            Err(_) => {
                *agent_process = None;
            }
        }
    }

    let agent_path = if cfg!(target_os = "windows") {
        let possible_paths = [
            "./aic_agent.exe",
            "../target/debug/aic_agent.exe",
            "../target/release/aic_agent.exe",
        ];

        let mut found_path = None;
        for path in &possible_paths {
            if std::path::Path::new(path).exists() {
                found_path = Some(path.to_string());
                break;
            }
        }

        found_path.ok_or_else(|| {
            "Agent executable not found. Please build the agent first."
        })?
    } else {
        let possible_paths = [
            "./aic_agent",
            "../target/debug/aic_agent",
            "../target/release/aic_agent",
        ];

        let mut found_path = None;
        for path in &possible_paths {
            if std::path::Path::new(path).exists() {
                found_path = Some(path.to_string());
                break;
            }
        }

        found_path.ok_or_else(|| {
            "Agent executable not found. Please build the agent first."
        })?
    };

    match Command::new(agent_path).spawn() {
        Ok(child) => {
            *agent_process = Some(child);
            log::info!("Agent started successfully");

            let app_handle = app_handle.clone();
            tokio::spawn(async move {
                tokio::time::sleep(tokio::time::Duration::from_millis(1000)).await;
                update_tray_icon_by_status(&app_handle, true).await;
            });

            Ok(true)
        }
        Err(e) => {
            log::error!("Failed to start agent: {}", e);
            Err(format!("Failed to start agent: {}", e))
        }
    }
}

// Provider config management commands
#[tauri::command]
async fn save_provider_configs(
    state: State<'_, AppState>,
    configs: Vec<aic_core::ProviderConfig>,
) -> Result<(), String> {
    state
        .config_loader
        .save_config(&configs)
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
async fn scan_for_api_keys(state: State<'_, AppState>) -> Result<Vec<aic_core::ProviderConfig>, String> {
    let configs = state.config_loader.load_config().await;
    Ok(configs)
}

#[tauri::command]
async fn check_github_login_status(state: State<'_, AppState>) -> Result<String, String> {
    use aic_core::TokenPollResult;

    let flow_state = state.device_flow_state.read().await;
    if let Some(ref flow) = *flow_state {
        match state.auth_manager.poll_for_token(&flow.device_code).await {
            TokenPollResult::Token(_) => Ok("success".to_string()),
            TokenPollResult::Pending => Ok("pending".to_string()),
            TokenPollResult::SlowDown => Ok("slow_down".to_string()),
            TokenPollResult::Expired => Err("Token expired".to_string()),
            TokenPollResult::AccessDenied => Err("Access denied".to_string()),
            TokenPollResult::Error(msg) => Err(format!("Error: {}", msg)),
        }
    } else {
        if state.auth_manager.is_authenticated() {
            Ok("success".to_string())
        } else {
            Err("No login flow".to_string())
        }
    }
}

#[tauri::command]
async fn discover_github_token() -> Result<TokenDiscoveryResult, String> {
    let mut found = false;
    let mut token = String::new();

    if let Ok(home) = std::env::var("HOME") {
        let gh_paths = [
            format!("{}/.config/gh/hosts.yml", home),
            format!("{}/.git-credential-store", home),
        ];

        for path in gh_paths.iter() {
            if std::path::Path::new(path).exists() {
                if let Ok(content) = std::fs::read_to_string(path) {
                    if let Some(pat) = extract_pat(&content) {
                        found = true;
                        token = pat;
                        break;
                    }
                }
            }
        }
    }

    Ok(TokenDiscoveryResult { found, token })
}

fn extract_pat(content: &str) -> Option<String> {
    if let Some(start) = content.find("github_pat_") {
        let rest = &content[start..];
        if let Some(end) = rest.find(|c: char| !c.is_alphanumeric() && c != '_' && c != '-') {
            Some(rest[..end].to_string())
        } else {
            None
        }
    } else {
        None
    }
}

// Update management commands
#[tauri::command]
async fn get_app_version(app: tauri::AppHandle) -> Result<String, String> {
    Ok(app.package_info().version.to_string())
}

#[tauri::command]
async fn check_for_updates(app: tauri::AppHandle) -> Result<UpdateCheckResult, String> {
    use tauri_plugin_updater::UpdaterExt;

    let current_version = app.package_info().version.to_string();
    
    match app.updater() {
        Ok(updater) => {
            match updater.check().await {
                Ok(Some(update)) => {
                    Ok(UpdateCheckResult {
                        current_version: current_version.clone(),
                        latest_version: update.version.clone(),
                        update_available: true,
                        download_url: update.download_url.to_string(),
                    })
                }
                Ok(None) => {
                    Ok(UpdateCheckResult {
                        current_version: current_version.clone(),
                        latest_version: current_version.clone(),
                        update_available: false,
                        download_url: String::new(),
                    })
                }
                Err(e) => {
                    Err(format!("Failed to check for updates: {}", e))
                }
            }
        }
        Err(e) => Err(format!("Updater not available: {}", e)),
    }
}

#[derive(serde::Serialize)]
pub struct UpdateCheckResult {
    pub current_version: String,
    pub latest_version: String,
    pub update_available: bool,
    pub download_url: String,
}

#[derive(serde::Serialize)]
pub struct TokenDiscoveryResult {
    pub found: bool,
    pub token: String,
}

#[tauri::command]
async fn install_update(app: tauri::AppHandle) -> Result<bool, String> {
    use tauri_plugin_updater::UpdaterExt;

    match app.updater() {
        Ok(updater) => {
            match updater.check().await {
                Ok(Some(update)) => {
                    // Download and install the update
                    match update.download_and_install(
                        |_, _| {}, // on_chunk callback
                        || {},      // on_download_finish callback
                    ).await {
                        Ok(_) => {
                            log::info!("Update installed successfully");
                            Ok(true)
                        }
                        Err(e) => {
                            log::error!("Failed to install update: {}", e);
                            Err(format!("Failed to install update: {}", e))
                        }
                    }
                }
                Ok(None) => {
                    Err("No update available".to_string())
                }
                Err(e) => {
                    Err(format!("Failed to check for updates: {}", e))
                }
            }
        }
        Err(e) => Err(format!("Updater not available: {}", e)),
    }
}

fn create_tray_menu<R: Runtime>(
    app: &tauri::AppHandle<R>,
) -> Result<Menu<R>, Box<dyn std::error::Error>> {
    let show_i = MenuItem::with_id(app, "show", "Show", true, None::<&str>)?;
    let refresh_i = MenuItem::with_id(app, "refresh", "Refresh", true, None::<&str>)?;
    let auto_refresh_i =
        MenuItem::with_id(app, "auto_refresh", "Auto Refresh", true, None::<&str>)?;
    let agent_start_i = MenuItem::with_id(app, "start_agent", "Start Agent", true, None::<&str>)?;
    let agent_stop_i = MenuItem::with_id(app, "stop_agent", "Stop Agent", true, None::<&str>)?;
    let settings_i = MenuItem::with_id(app, "settings", "Settings", true, None::<&str>)?;
    let quit_i = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;

    let menu = Menu::with_items(
        app,
        &[
            &show_i,
            &refresh_i,
            &auto_refresh_i,
            &MenuItem::with_id(app, "separator1", "---", false, None::<&str>)?,
            &agent_start_i,
            &agent_stop_i,
            &MenuItem::with_id(app, "separator2", "---", false, None::<&str>)?,
            &settings_i,
            &MenuItem::with_id(app, "separator3", "---", false, None::<&str>)?,
            &quit_i,
        ],
    )?;

    Ok(menu)
}

#[tokio::main]
async fn main() {
    let client = reqwest::Client::new();
    let provider_manager = Arc::new(ProviderManager::new(client.clone()));
    let config_loader = Arc::new(ConfigLoader::new(client.clone()));
    let auth_service = Arc::new(GitHubAuthService::new(client));
    let auth_manager = Arc::new(AuthenticationManager::new(
        auth_service.clone(),
        config_loader.clone(),
    ));

    // Initialize auth manager from existing config
    let auth_manager_clone = auth_manager.clone();
    tokio::spawn(async move {
        auth_manager_clone.initialize_from_config().await;
    });

    // Start auto-refresh background task
    let auto_refresh_enabled = Arc::new(Mutex::new(false));
    let manager_clone = provider_manager.clone();
    let auto_refresh_clone = auto_refresh_enabled.clone();

    tokio::spawn(async move {
        let mut interval = interval(Duration::from_secs(300)); // 5 minutes

        loop {
            interval.tick().await;

            let enabled = *auto_refresh_clone.lock().await;
            if enabled {
                // Refresh usage in background
                let _ = manager_clone.get_all_usage(true).await;
            }
        }
    });

    tauri::Builder::default()
        .manage(AppState {
            provider_manager,
            config_loader,
            auth_manager,
            auto_refresh_enabled,
            device_flow_state: Arc::new(RwLock::new(None)),
            agent_process: Arc::new(Mutex::new(None)),
        })
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .invoke_handler(tauri::generate_handler![
            // Provider commands
            get_usage,
            refresh_usage,
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
            cancel_github_login,
            // Window control commands
            close_window,
            minimize_window,
            toggle_always_on_top,
            // Browser command
            open_browser,
            // Settings commands
            close_settings_window,
            open_settings_window,
            save_provider_configs,
            scan_for_api_keys,
            check_github_login_status,
            discover_github_token,
            // Agent management commands
            start_agent,
            stop_agent,
            is_agent_running,
            get_agent_status_details,
            get_all_providers_from_agent,
            // Update management commands
            get_app_version,
            check_for_updates,
            install_update,
        ])
        .setup(|app| {
            // Create tray menu
            let menu = create_tray_menu(app.handle())?;

            // Build tray icon
            let _tray = TrayIconBuilder::new()
                .menu(&menu)
                .tooltip("AI Consumption Tracker")

                .on_menu_event(move |app, event| {
                    match event.id().as_ref() {
                        "show" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _: Result<(), _> = window.show();
                                let _: Result<(), _> = window.set_focus();
                            }
                        }
                        "refresh" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _: Result<(), _> = window.emit("refresh-requested", ());
                            }
                        }
                        "auto_refresh" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _: Result<(), _> = window.emit("toggle-auto-refresh", ());
                            }
                        }
                        "start_agent" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _: Result<(), _> = window.emit("start-agent", ());
                            }
                        }
                        "stop_agent" => {
                            if let Some(window) = app.get_webview_window("main") {
                                let _: Result<(), _> = window.emit("stop-agent", ());
                            }
                        }
                        "settings" => {
                            let _: Result<(), _> = app.emit("open-settings-window", ());
                        }
                        "quit" => {
                            app.exit(0);
                        }
                        _ => {}
                    }
                })
                .build(app)?;

            // Initial tray icon status check
            let app_handle = app.handle().clone();
            tokio::spawn(async move {
                tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;
                check_and_update_tray_status(&app_handle).await;
            });

            // Periodic tray icon status update (every 30 seconds)
            let app_handle = app.handle().clone();
            tokio::spawn(async move {
                loop {
                    tokio::time::sleep(tokio::time::Duration::from_secs(30)).await;
                    check_and_update_tray_status(&app_handle).await;
                }
            });

            // Ensure main window is shown
            if let Some(window) = app.get_webview_window("main") {
                window.show()?;
                window.set_focus()?;
                println!("Main window shown successfully");
            } else {
                println!("WARNING: Main window not found!");
            }

            // Check for updates on startup (silent)
            let app_handle = app.handle().clone();
            tokio::spawn(async move {
                // Wait a moment for app to fully initialize
                tokio::time::sleep(tokio::time::Duration::from_secs(5)).await;
                
                if let Ok(updater) = app_handle.updater() {
                    match updater.check().await {
                        Ok(Some(update)) => {
                            log::info!("Update available: v{}", update.version);
                            // Optionally show notification or update tray menu
                        }
                        Ok(None) => {
                            log::debug!("No updates available");
                        }
                        Err(e) => {
                            log::error!("Failed to check for updates on startup: {}", e);
                        }
                    }
                }
            });

            // Do startup discovery once
            let config_loader = app.state::<AppState>().config_loader.clone();
            tokio::spawn(async move {
                tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;

                log::info!("Performing startup configuration discovery...");
                let configs = config_loader.load_config().await;
                log::info!("Startup discovery found {} provider configurations", configs.len());
            });

            // Auto-start agent if not running
            let app_handle = app.handle().clone();
            let agent_process = app.state::<AppState>().agent_process.clone();
            tokio::spawn(async move {
                tokio::time::sleep(tokio::time::Duration::from_secs(2)).await;
                
                log::info!("Checking if agent is running on startup...");
                let is_running = match check_agent_status().await {
                    Ok(running) => running,
                    Err(e) => {
                        log::error!("Failed to check agent status: {}", e);
                        false
                    }
                };

                if !is_running {
                    log::info!("Agent not running, starting automatically...");
                    match start_agent_internal(&app_handle, agent_process).await {
                        Ok(started) => {
                            if started {
                                log::info!("Agent started successfully");
                            } else {
                                log::warn!("Agent failed to start");
                            }
                        }
                        Err(e) => {
                            log::error!("Failed to start agent: {}", e);
                        }
                    }
                }
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application")
}