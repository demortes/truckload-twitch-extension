import { Hono } from "hono";
import type { Env } from "../env";
import { MAX_BODY_BYTES, canonicalJson, parsePayload, validate } from "../lib/contract";
import { errorJson, partitionKeyFor, readBodyLimited } from "../lib/http";
import { findChannelIdByKey, suppressBroadcast, upsertTelemetry } from "../lib/store";
import { broadcast } from "../lib/twitch";

export const ingest = new Hono<{ Bindings: Env }>();

const DEFAULT_MIN_BROADCAST_INTERVAL_MS = 1000;

function record(outcome: string, extra: Record<string, unknown> = {}) {
  // One structured line per ingest outcome; Workers Observability indexes these fields.
  console.log(JSON.stringify({ event: "ingest", outcome, ...extra }));
}

const tooLargeMessage = `Request body exceeds ${MAX_BODY_BYTES} bytes`;

ingest.post("/", async (c) => {
  // Header only. A key in the query string would be written to proxy and CDN access logs.
  const apiKey = c.req.header("X-Api-Key");

  const limit = await c.env.INGEST_LIMITER.limit({ key: partitionKeyFor(apiKey) });
  if (!limit.success) {
    record("rate_limited");
    c.header("Retry-After", "1");
    return errorJson(c, 429, "Too many requests. Slow down and retry shortly.");
  }

  if (!apiKey) {
    record("missing_key");
    return errorJson(c, 401, "Missing API key");
  }

  const channelId = await findChannelIdByKey(c.env.DB, apiKey);
  if (channelId === null) {
    record("invalid_key");
    return errorJson(c, 401, "Invalid API key");
  }

  const declaredLength = Number(c.req.header("Content-Length"));
  if (Number.isFinite(declaredLength) && declaredLength > MAX_BODY_BYTES) {
    record("too_large");
    return errorJson(c, 413, tooLargeMessage);
  }

  const body = await readBodyLimited(c.req.raw, MAX_BODY_BYTES);
  if (!body.ok) {
    record("too_large");
    return errorJson(c, 413, tooLargeMessage);
  }
  if (body.bytes.length === 0) {
    record("empty");
    return errorJson(c, 400, "Empty request body");
  }

  const parsed = parsePayload(new TextDecoder().decode(body.bytes));
  if (!parsed.ok) {
    record("malformed", { channelId });
    return errorJson(c, 400, "Invalid telemetry payload");
  }

  const errors = validate(parsed.payload);
  if (errors.length > 0 || parsed.payload === null) {
    record("invalid");
    return errorJson(c, 400, "Telemetry payload failed validation", errors.join(" "));
  }

  const canonical = canonicalJson(parsed.payload);
  const minInterval = Number.parseInt(c.env.MIN_BROADCAST_INTERVAL_MS ?? "", 10);
  const nowMs = Date.now();

  const { broadcastAcquired } = await upsertTelemetry(
    c.env.DB,
    channelId,
    parsed.payload,
    canonical,
    nowMs,
    Number.isFinite(minInterval) ? minInterval : DEFAULT_MIN_BROADCAST_INTERVAL_MS,
  );
  record("stored", { bytes: body.bytes.length });

  if (!broadcastAcquired) return c.json({ stored: true, broadcast: "throttled" });

  const result = await broadcast(c.env, channelId, canonical);
  record("broadcast", { result: result.outcome });

  if (result.outcome === "rate_limited" && result.retryAfterMs !== undefined)
    await suppressBroadcast(c.env.DB, channelId, nowMs + result.retryAfterMs);

  return c.json({ stored: true, broadcast: result.outcome, retryAfterMs: result.retryAfterMs ?? null });
});