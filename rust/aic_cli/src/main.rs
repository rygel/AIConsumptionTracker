use aic_core::{AuthenticationManager, ConfigLoader, GitHubAuthService, ProviderManager};
use clap::{Parser, Subcommand};
use std::process::Command;
use std::sync::Arc;

#[derive(Parser)]
#[command(name = "opencode-tracker")]
#[command(about = "AI Consumption Tracker CLI")]
struct Cli {
    #[command(subcommand)]
    command: Option<Commands>,

    /// Show all providers even if not configured
    #[arg(long, global = true)]
    all: bool,

    /// Output as JSON
    #[arg(long, global = true)]
    json: bool,

    /// Verbose output
    #[arg(short, long, global = true)]
    verbose: bool,
}

#[derive(Subcommand)]
enum Commands {
    /// Show usage status
    Status,
    /// List configured providers
    List,
    /// Authenticate with a provider
    Auth {
        /// Provider to authenticate with
        provider: String,
    },
    /// Logout from a provider
    Logout {
        /// Provider to logout from
        provider: String,
    },
}

#[tokio::main]
async fn main() {
    let cli = Cli::parse();

    // Initialize logging
    if cli.verbose {
        env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("debug")).init();
    } else {
        env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("warn")).init();
    }

    let command = cli.command.unwrap_or_else(|| {
        print_usage();
        std::process::exit(0);
    });

    match command {
        Commands::Status => {
            show_status(cli.all, cli.json, cli.verbose).await;
        }
        Commands::List => {
            show_list(cli.json).await;
        }
        Commands::Auth { provider } => {
            handle_auth(&provider).await;
        }
        Commands::Logout { provider } => {
            handle_logout(&provider).await;
        }
    }
}

fn print_usage() {
    println!("Usage: opencode-tracker <command> [options]");
    println!();
    println!("Commands:");
    println!("  status    Show usage status");
    println!("    --all   Show all providers even if not configured");
    println!("    --json  Output as JSON");
    println!("    -v      Verbose output");
    println!("  list      List configured providers");
    println!("  auth      Authenticate with a provider");
    println!("    github  Authenticate with GitHub Copilot");
    println!("  logout    Logout from a provider");
    println!("    github  Logout from GitHub Copilot");
}

async fn show_status(show_all: bool, json: bool, verbose: bool) {
    let client = reqwest::Client::new();
    let manager = ProviderManager::new(client);

    let usage = manager.get_all_usage(true).await;

    let filtered_usage: Vec<_> = if show_all {
        usage
    } else {
        usage.into_iter().filter(|u| u.is_available).collect()
    };

    if json {
        match serde_json::to_string_pretty(&filtered_usage) {
            Ok(json_str) => println!("{}", json_str),
            Err(e) => eprintln!("Error serializing to JSON: {}", e),
        }
    } else {
        // Sort alphabetically
        let mut sorted: Vec<_> = filtered_usage.into_iter().collect();
        sorted.sort_by(|a, b| {
            a.provider_name
                .to_lowercase()
                .cmp(&b.provider_name.to_lowercase())
        });

        println!(
            "{:<36} | {:<14} | {:<10} | {}",
            "Provider", "Type", "Used", "Description"
        );
        println!("{}", "-".repeat(98));

        if sorted.is_empty() {
            println!("No active providers found.");
        }

        for u in sorted {
            let pct = if u.is_available {
                format!("{:.0}%", u.usage_percentage)
            } else {
                "-".to_string()
            };
            let type_str = if u.is_quota_based {
                "Quota"
            } else {
                "Pay-As-You-Go"
            };
            let account_info = if u.account_name.is_empty() {
                String::new()
            } else {
                format!(" [{}]", u.account_name)
            };

            let description = if u.description.is_empty() {
                account_info.trim().to_string()
            } else {
                format!("{}{}", u.description, account_info)
            };

            let lines: Vec<&str> = description.lines().collect();

            if lines.is_empty() {
                println!(
                    "{:<36} | {:<14} | {:<10} | {}",
                    u.provider_name, type_str, pct, ""
                );
            } else {
                println!(
                    "{:<36} | {:<14} | {:<10} | {}",
                    u.provider_name, type_str, pct, lines[0]
                );

                for line in &lines[1..] {
                    println!("{:<36} | {:<14} | {:<10} | {}", "", "", "", line);
                }
            }

            if verbose {
                println!(
                    "{:<36} | {:<14} | {:<10} |   Unit: {}",
                    "", "", "", u.usage_unit
                );
                if let Some(reset_time) = u.next_reset_time {
                    println!(
                        "{:<36} | {:<14} | {:<10} |   Reset: {}",
                        "", "", "", reset_time
                    );
                }
                println!(
                    "{:<36} | {:<14} | {:<10} |   Auth: {}",
                    "", "", "", u.auth_source
                );
                if u.cost_limit > 0.0 {
                    println!(
                        "{:<36} | {:<14} | {:<10} |   Cost: {}/{}",
                        "", "", "", u.cost_used, u.cost_limit
                    );
                }
            }

            if let Some(details) = &u.details {
                for d in details {
                    let name = format!("  {}", d.name);
                    println!(
                        "{:<36} | {:<14} | {:<10} | {}",
                        name, "", d.used, d.description
                    );
                }
            }
        }
    }
}

async fn show_list(json: bool) {
    let client = reqwest::Client::new();
    let config_loader = ConfigLoader::new(client);
    let configs = config_loader.load_config().await;

    if json {
        match serde_json::to_string_pretty(&configs) {
            Ok(json_str) => println!("{}", json_str),
            Err(e) => eprintln!("Error serializing to JSON: {}", e),
        }
    } else {
        for c in configs {
            println!("ID: {}, Type: {}", c.provider_id, c.config_type);
        }
    }
}

async fn handle_auth(provider: &str) {
    if provider.to_lowercase() != "github" {
        println!("Unknown provider for auth: {}", provider);
        println!("Supported providers: github");
        return;
    }

    let client = reqwest::Client::new();
    let auth_service = Arc::new(GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(ConfigLoader::new(client));
    let auth_manager = AuthenticationManager::new(auth_service.clone(), config_loader.clone());

    // Initialize from existing config if available
    auth_manager.initialize_from_config().await;

    if auth_manager.is_authenticated() {
        println!("Already authenticated with GitHub.");
        print!("Would you like to re-authenticate? [y/N]: ");
        use std::io::{self, Write};
        let _ = io::stdout().flush();
        let mut input = String::new();
        if io::stdin().read_line(&mut input).is_ok() {
            if !input.trim().eq_ignore_ascii_case("y") {
                println!("Authentication cancelled.");
                return;
            }
        }
    }

    println!("Initiating GitHub Device Flow...\n");

    match auth_manager.initiate_login().await {
        Ok(device_flow) => {
            println!("Please visit: {}", device_flow.verification_uri);
            println!("Enter the following code: {}\n", device_flow.user_code);

            // Try to open browser
            open_browser(&device_flow.verification_uri);

            println!("Waiting for authentication...");

            // Wait for login with automatic polling
            match auth_manager
                .wait_for_login(&device_flow.device_code, device_flow.interval as u64)
                .await
            {
                Ok(true) => {
                    println!("\n✓ Successfully authenticated with GitHub!");
                    println!("GitHub Copilot provider is now active.");
                }
                Ok(false) => {
                    println!("\n✗ Authentication failed or was cancelled.");
                    std::process::exit(1);
                }
                Err(e) => {
                    println!("\n✗ Authentication error: {}", e);
                    std::process::exit(1);
                }
            }
        }
        Err(e) => {
            eprintln!("Failed to initiate device flow: {}", e);
            std::process::exit(1);
        }
    }
}

async fn handle_logout(provider: &str) {
    if provider.to_lowercase() != "github" {
        println!("Unknown provider for logout: {}", provider);
        println!("Supported providers: github");
        return;
    }

    let client = reqwest::Client::new();
    let auth_service = Arc::new(GitHubAuthService::new(client.clone()));
    let config_loader = Arc::new(ConfigLoader::new(client));
    let auth_manager = AuthenticationManager::new(auth_service.clone(), config_loader.clone());

    // Initialize from existing config
    auth_manager.initialize_from_config().await;

    if !auth_manager.is_authenticated() {
        println!("Not currently authenticated with GitHub.");
        return;
    }

    match auth_manager.logout().await {
        Ok(_) => {
            println!("✓ Successfully logged out from GitHub.");
        }
        Err(e) => {
            eprintln!("✗ Failed to logout: {}", e);
            std::process::exit(1);
        }
    }
}

fn open_browser(url: &str) {
    #[cfg(target_os = "windows")]
    {
        let _ = Command::new("cmd").args(["/C", "start", url]).spawn();
    }
    #[cfg(target_os = "macos")]
    {
        let _ = Command::new("open").arg(url).spawn();
    }
    #[cfg(target_os = "linux")]
    {
        let _ = Command::new("xdg-open").arg(url).spawn();
    }
}
