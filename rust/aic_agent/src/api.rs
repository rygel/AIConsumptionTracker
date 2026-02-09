use std::sync::Arc;
use actix_web::{web, App, HttpServer, HttpResponse};
use log::info;
use actix_web::http::header::ContentType;
use chrono::{Utc, Duration as ChronoDuration};
use std::time::Duration as StdDuration;
use reqwest::Client;
use aic_core::{PaymentType, ProviderUsage, ConfigLoader};

use super::{AgentState, ApiResponse, UsageSummary, ProviderSummary};

async fn health_check() -> HttpResponse {
    HttpResponse::Ok()
        .content_type(ContentType::json())
        .json(ApiResponse::success("OK"))
}

async fn get_status(state: web::Data<Arc<AgentState>>) -> HttpResponse {
    let last_refresh = *state.last_refresh.lock().await;
    let config = state.config.lock().await;
    let interval = config.polling_interval_seconds;

    let next_refresh = last_refresh.map(|lr| lr + ChronoDuration::seconds(interval as i64));

    let status = super::AgentStatus {
        running: true,
        polling_enabled: config.polling_enabled,
        polling_interval_seconds: interval,
        last_refresh,
        next_refresh,
        database_path: config.database_path.clone(),
        configured_providers: vec![],
        active_providers: vec![],
    };

    HttpResponse::Ok()
        .content_type(ContentType::json())
        .json(ApiResponse::success(status))
}

async fn refresh(state: web::Data<Arc<AgentState>>) -> HttpResponse {
    let is_polling = state.is_polling.lock().await;
    if *is_polling {
        return HttpResponse::Ok()
            .content_type(ContentType::json())
            .json(ApiResponse::<()>::error("Refresh already in progress".to_string()));
    }
    drop(is_polling);

    let mut is_polling_guard = state.is_polling.lock().await;
    *is_polling_guard = true;
    drop(is_polling_guard);

    let client = Client::new();
    let config_loader = ConfigLoader::new(client);
    let configs = config_loader.load_config().await;

    let mut all_usages = Vec::new();

    for config in configs {
        let usage = ProviderUsage {
            provider_id: config.provider_id.clone(),
            provider_name: config.provider_id.clone(),
            usage_percentage: 0.0,
            cost_used: 0.0,
            cost_limit: config.limit.unwrap_or(100.0),
            payment_type: PaymentType::UsageBased,
            usage_unit: "unknown".to_string(),
            is_quota_based: false,
            is_available: false,
            description: format!("Configured: {}", config.provider_id),
            auth_source: config.auth_source,
            details: None,
            account_name: "unknown".to_string(),
            next_reset_time: None,
        };
        all_usages.push(usage);
    }

    if !all_usages.is_empty() {
        if let Err(e) = state.db.store_usage(all_usages.clone()).await {
            let mut is_polling_guard = state.is_polling.lock().await;
            *is_polling_guard = false;
            return HttpResponse::InternalServerError()
                .content_type(ContentType::json())
                .json(ApiResponse::<()>::error(e.to_string()));
        }
    }

    let mut last_refresh_guard = state.last_refresh.lock().await;
    *last_refresh_guard = Some(Utc::now());
    drop(last_refresh_guard);

    let mut is_polling_guard = state.is_polling.lock().await;
    *is_polling_guard = false;

    HttpResponse::Ok()
        .content_type(ContentType::json())
        .json(ApiResponse::success(all_usages.len()))
}

async fn shutdown(_state: web::Data<Arc<AgentState>>) -> HttpResponse {
    info!("Shutdown requested via API");

    tokio::spawn(async move {
        tokio::time::sleep(std::time::Duration::from_secs(1)).await;
        std::process::exit(0);
    });

    HttpResponse::Ok()
        .content_type(ContentType::json())
        .json(ApiResponse::success("Shutting down..."))
}

async fn get_usage(state: web::Data<Arc<AgentState>>) -> HttpResponse {
    match state.db.get_latest_usage().await {
        Ok(records) => {
            let summary = UsageSummary {
                total_providers: records.len(),
                active_providers: records.iter().filter(|r| r.is_available).count(),
                total_usage_records: records.len() as i64,
                records_today: records.len() as i64,
                providers: records.iter().map(|r| ProviderSummary {
                    provider_id: r.provider_id.clone(),
                    provider_name: r.provider_name.clone(),
                    is_available: r.is_available,
                    usage_percentage: r.usage_percentage,
                    cost_used: r.cost_used,
                    cost_limit: r.cost_limit,
                    last_updated: Some(r.recorded_at),
                }).collect(),
            };
            HttpResponse::Ok()
                .content_type(ContentType::json())
                .json(ApiResponse::success(summary))
        }
        Err(e) => HttpResponse::InternalServerError()
            .content_type(ContentType::json())
            .json(ApiResponse::<()>::error(e.to_string())),
    }
}

async fn get_usage_history(
    state: web::Data<Arc<AgentState>>,
    query: web::Query<std::collections::HashMap<String, String>>,
) -> HttpResponse {
    let provider_id = query.get("provider_id").map(|s| s.as_str());
    let limit = query.get("limit").and_then(|s| s.parse().ok());

    match state.db.get_usage_history(provider_id, None, None, limit).await {
        Ok(records) => HttpResponse::Ok()
            .content_type(ContentType::json())
            .json(ApiResponse::success(records)),
        Err(e) => HttpResponse::InternalServerError()
            .content_type(ContentType::json())
            .json(ApiResponse::<()>::error(e.to_string())),
    }
}

async fn get_statistics(state: web::Data<Arc<AgentState>>) -> HttpResponse {
    match state.db.get_statistics().await {
        Ok(stats) => HttpResponse::Ok()
            .content_type(ContentType::json())
            .json(ApiResponse::success(stats)),
        Err(e) => HttpResponse::InternalServerError()
            .content_type(ContentType::json())
            .json(ApiResponse::<()>::error(e.to_string())),
    }
}

async fn get_aggregated_usage(
    state: web::Data<Arc<AgentState>>,
    query: web::Query<std::collections::HashMap<String, String>>,
) -> HttpResponse {
    let provider_id = query.get("provider_id").map(|s| s.as_str());
    let period = query.get("period").map_or("day", |v| v.as_str());

    let end_time = Utc::now();
    let start_time = end_time - ChronoDuration::days(1);

    match state.db.get_aggregated_usage(provider_id, start_time, end_time, period).await {
        Ok(records) => HttpResponse::Ok()
            .content_type(ContentType::json())
            .json(ApiResponse::success(records)),
        Err(e) => HttpResponse::InternalServerError()
            .content_type(ContentType::json())
            .json(ApiResponse::<()>::error(e.to_string())),
    }
}

pub async fn start_server(state: AgentState, listen_address: String) -> std::io::Result<()> {
    let state = Arc::new(state);
    let state = web::Data::new(state);

    info!("Starting HTTP API server on http://{}", listen_address);

    HttpServer::new(move || {
        App::new()
            .app_data(state.clone())
            .route("/health", web::get().to(health_check))
            .route("/api/v1/status", web::get().to(get_status))
            .route("/api/v1/refresh", web::post().to(refresh))
            .route("/api/v1/shutdown", web::post().to(shutdown))
            .route("/api/v1/usage", web::get().to(get_usage))
            .route("/api/v1/usage/history", web::get().to(get_usage_history))
            .route("/api/v1/statistics", web::get().to(get_statistics))
            .route("/api/v1/aggregated", web::get().to(get_aggregated_usage))
    })
    .bind(&listen_address)?
    .run()
    .await
}
