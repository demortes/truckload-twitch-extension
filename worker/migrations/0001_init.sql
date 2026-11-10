-- Translated from the EF Core model in backend/Truckload.Ebs (Channels, TelemetrySnapshots,
-- JobHistoryEntries). Timestamps are ISO-8601 UTC text, which sorts lexicographically.

CREATE TABLE channels (
  channel_id TEXT PRIMARY KEY,
  ingest_key TEXT NOT NULL UNIQUE,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

-- The latest whole-state payload per channel. The two integer columns are the per-channel PubSub
-- broadcast throttle (epoch milliseconds), kept in the row the ingest upsert already writes, so
-- throttling costs no extra database write and is safe across Worker isolates.
CREATE TABLE telemetry_snapshots (
  channel_id TEXT PRIMARY KEY REFERENCES channels (channel_id) ON DELETE CASCADE,
  payload_json TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  last_broadcast_at INTEGER NOT NULL DEFAULT 0,
  broadcast_suppressed_until INTEGER NOT NULL DEFAULT 0
);

-- Recent completed jobs, capped per channel by the ingest path (session history, not an archive).
CREATE TABLE job_history (
  id TEXT PRIMARY KEY,
  channel_id TEXT NOT NULL REFERENCES channels (channel_id) ON DELETE CASCADE,
  cargo TEXT NOT NULL,
  source TEXT NOT NULL,
  destination TEXT NOT NULL,
  distance INTEGER NOT NULL,
  completed_at TEXT NOT NULL
);

CREATE INDEX ix_job_history_channel_completed ON job_history (channel_id, completed_at);
