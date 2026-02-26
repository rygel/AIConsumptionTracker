-- Add response latency tracking per provider history sample

ALTER TABLE provider_history ADD COLUMN response_latency_ms REAL NOT NULL DEFAULT 0;
