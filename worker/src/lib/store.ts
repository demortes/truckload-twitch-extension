// D1 access for channels, the latest telemetry snapshot, and recent completed jobs.
// Replaces ChannelKeyService, TelemetryService and the EF model in backend/Truckload.Ebs.

import { parsePayload, type TelemetryPayload } from "./contract";

/** Session-only retention: only the most recent completed jobs per channel are kept. */
export const MAX_JOB_HISTORY_ROWS_PER_CHANNEL = 20;

const NOW_SQL = "strftime('%Y-%m-%dT%H:%M:%fZ', 'now')";

function randomKey(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

export async function findChannelIdByKey(db: D1Database, apiKey: string): Promise<string | null> {
  const row = await db.prepare("SELECT channel_id FROM channels WHERE ingest_key = ?1").bind(apiKey).first<{ channel_id: string }>();
  return row?.channel_id ?? null;
}

export async function getKey(db: D1Database, channelId: string): Promise<string | null> {
  const row = await db.prepare("SELECT ingest_key FROM channels WHERE channel_id = ?1").bind(channelId).first<{ ingest_key: string }>();
  return row?.ingest_key ?? null;
}

/** Creates the channel or replaces its key (the old key stops working immediately). */
export async function generateKey(db: D1Database, channelId: string): Promise<string> {
  const key = randomKey();
  await db
    .prepare(
      `INSERT INTO channels (channel_id, ingest_key) VALUES (?1, ?2)
       ON CONFLICT (channel_id) DO UPDATE SET ingest_key = excluded.ingest_key, updated_at = ${NOW_SQL}`,
    )
    .bind(channelId, key)
    .run();
  return key;
}

export async function getLatest(db: D1Database, channelId: string): Promise<string | null> {
  const row = await db.prepare("SELECT payload_json FROM telemetry_snapshots WHERE channel_id = ?1").bind(channelId).first<{ payload_json: string }>();
  return row?.payload_json ?? null;
}

export interface HistoryRow {
  id: string;
  cargo: string;
  source: string;
  destination: string;
  distance: number;
  completedAt: string;
}

export async function getHistory(db: D1Database, channelId: string, limit: number): Promise<HistoryRow[]> {
  const { results } = await db
    .prepare(
      `SELECT id, cargo, source, destination, distance, completed_at AS completedAt
       FROM job_history WHERE channel_id = ?1 ORDER BY completed_at DESC LIMIT ?2`,
    )
    .bind(channelId, limit)
    .all<HistoryRow>();
  return results;
}

function tryGetJob(payloadJson: string) {
  const parsed = parsePayload(payloadJson);
  return parsed.ok ? (parsed.payload?.job ?? null) : null;
}

export interface UpsertResult {
  /** True when this ingest won the per-channel broadcast throttle and should be relayed over PubSub. */
  broadcastAcquired: boolean;
}

/**
 * Stores the new snapshot, archives the previous job to history if it just completed (it was active
 * and the incoming one is not), and decides whether this ingest may broadcast. The bridge only sends
 * whole-state snapshots, so a completed job is detected here by comparing against what is stored.
 *
 * The snapshot upsert also claims the per-channel broadcast slot: last_broadcast_at moves to `nowMs`
 * only when the minimum interval has elapsed and no suppression is active, and the returned value
 * says which happened. One statement, no extra write, and correct across concurrent isolates.
 */
export async function upsertTelemetry(
  db: D1Database,
  channelId: string,
  payload: TelemetryPayload,
  canonicalJson: string,
  nowMs: number,
  minBroadcastIntervalMs: number,
): Promise<UpsertResult> {
  const nowIso = new Date(nowMs).toISOString();

  const previous = await db.prepare("SELECT payload_json FROM telemetry_snapshots WHERE channel_id = ?1").bind(channelId).first<{ payload_json: string }>();
  const previousJob = previous ? tryGetJob(previous.payload_json) : null;

  const statements: D1PreparedStatement[] = [];

  if (previousJob?.active && !payload.job?.active) {
    statements.push(
      db
        .prepare(
          `INSERT INTO job_history (id, channel_id, cargo, source, destination, distance, completed_at)
           VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)`,
        )
        .bind(crypto.randomUUID(), channelId, previousJob.cargo ?? "", previousJob.source ?? "", previousJob.destination ?? "", previousJob.distance, nowIso),
      db
        .prepare(
          `DELETE FROM job_history WHERE channel_id = ?1 AND id NOT IN (
             SELECT id FROM job_history WHERE channel_id = ?1 ORDER BY completed_at DESC LIMIT ?2)`,
        )
        .bind(channelId, MAX_JOB_HISTORY_ROWS_PER_CHANNEL),
    );
  }

  statements.push(
    db
      .prepare(
        `INSERT INTO telemetry_snapshots (channel_id, payload_json, updated_at, last_broadcast_at)
         VALUES (?1, ?2, ?3, ?4)
         ON CONFLICT (channel_id) DO UPDATE SET
           payload_json = excluded.payload_json,
           updated_at = excluded.updated_at,
           last_broadcast_at = CASE
             WHEN ?4 - telemetry_snapshots.last_broadcast_at >= ?5 AND ?4 >= telemetry_snapshots.broadcast_suppressed_until
             THEN ?4 ELSE telemetry_snapshots.last_broadcast_at END
         RETURNING last_broadcast_at`,
      )
      .bind(channelId, canonicalJson, nowIso, nowMs, minBroadcastIntervalMs),
  );

  const results = await db.batch<{ last_broadcast_at: number }>(statements);
  const upsert = results[results.length - 1];
  return { broadcastAcquired: upsert?.results[0]?.last_broadcast_at === nowMs };
}

/** After Twitch rate limits a channel, leave it alone until `untilMs`. */
export async function suppressBroadcast(db: D1Database, channelId: string, untilMs: number): Promise<void> {
  await db.prepare("UPDATE telemetry_snapshots SET broadcast_suppressed_until = ?2 WHERE channel_id = ?1").bind(channelId, untilMs).run();
}