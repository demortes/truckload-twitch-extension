-- Identifies which ingest last claimed the per-channel broadcast slot. Comparing timestamps could not
-- tell two ingests in the same millisecond apart; a per-request random token can.
ALTER TABLE telemetry_snapshots ADD COLUMN last_broadcast_token TEXT;