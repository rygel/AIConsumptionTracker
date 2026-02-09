use std::env;
use std::path::PathBuf;
use clap::{Parser, Subcommand};
use log::info;

use aic_agent::{AgentConfig, UsageAgent};

#[derive(Parser, Debug)]
#[command(name = "aic-agent")]
#[command(author, version, about, long_about = None)]
struct Args {
    #[command(subcommand)]
    command: Option<Commands>,

    #[arg(short, long)]
    config: Option<PathBuf>,

    #[arg(short, long)]
    database: Option<PathBuf>,

    #[arg(short, long)]
    listen: Option<String>,

    #[arg(short, long)]
    interval: Option<u64>,

    #[arg(short, long)]
    foreground: bool,
}

#[derive(Subcommand, Debug)]
enum Commands {
    Start,
    Stop,
    Status,
    Refresh,
    Config,
}

fn setup_logging(level: &str) {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or(level))
        .format_timestamp(None)
        .init();
}

#[tokio::main]
async fn main() {
    let args = Args::parse();

    let mut config = AgentConfig::load().unwrap_or_default();

    if let Some(database) = args.database {
        config.database_path = database;
    }

    if let Some(listen) = args.listen {
        config.listen_address = listen;
    }

    if let Some(interval) = args.interval {
        config.polling_interval_seconds = interval;
    }

    match args.command {
        Some(Commands::Start) => {
            setup_logging(&config.log_level);
            info!("Starting AIConsumptionTracker Agent...");

            let agent = UsageAgent::new(config).await.unwrap();
            agent.run().await.unwrap();
        }

        Some(Commands::Refresh) => {
            setup_logging("info");

            if let Err(e) = reqwest::Client::new()
                .post(&format!("http://{}/api/v1/refresh", config.listen_address))
                .send()
                .await
            {
                eprintln!("Failed to connect to agent: {}", e);
                std::process::exit(1);
            }
        }

        Some(Commands::Status) => {
            if let Err(e) = reqwest::Client::new()
                .get(&format!("http://{}/api/v1/status", config.listen_address))
                .send()
                .await
            {
                eprintln!("Failed to connect to agent: {}", e);
                std::process::exit(1);
            }
        }

        Some(Commands::Config) => {
            println!("{}", toml::to_string_pretty(&config).unwrap());
        }

        Some(Commands::Stop) => {
            info!("Sending stop signal to agent...");

            if let Err(e) = reqwest::Client::new()
                .post(&format!("http://{}/api/v1/shutdown", config.listen_address))
                .send()
                .await
            {
                eprintln!("Failed to connect to agent: {}", e);
                std::process::exit(1);
            }
        }

        None => {
            println!("No command specified. Use --help for usage information.");
        }
    }
}
