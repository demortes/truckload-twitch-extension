import { Hono } from "hono";
import type { Env } from "../env";
import { errorJson, type AppContext } from "../lib/http";
import { generateKey, getKey } from "../lib/store";
import { verifyJwt } from "../lib/twitch";

export const channels = new Hono<{ Bindings: Env }>();

const INGEST_URL = "/api/ingest";

type Verified = { channelId: string } | { error: Response };

/** Requires a Twitch extension JWT issued to the channel's broadcaster. */
async function verifyBroadcaster(c: AppContext): Promise<Verified> {
  const authHeader = c.req.header("Authorization");
  if (!authHeader || !authHeader.startsWith("Bearer "))
    return { error: errorJson(c, 401, "Missing Authorization header") };

  const result = await verifyJwt(c.env.TWITCH_EXTENSION_SECRET, authHeader.slice("Bearer ".length));
  if (!result.ok) return { error: errorJson(c, 401, "Invalid or expired token") };

  const channelId = result.claims.channel_id;
  if (typeof channelId !== "string" || channelId === "")
    return { error: errorJson(c, 401, "Invalid token: missing channel_id") };

  if (result.claims.role !== "broadcaster") return { error: errorJson(c, 403, "Only broadcasters can manage keys") };

  const limit = await c.env.KEYS_LIMITER.limit({ key: channelId });
  if (!limit.success) {
    c.header("Retry-After", "1");
    return { error: errorJson(c, 429, "Too many requests. Slow down and retry shortly.") };
  }

  return { channelId };
}

channels.get("/keys", async (c) => {
  const verified = await verifyBroadcaster(c);
  if ("error" in verified) return verified.error;

  const ingestKey = await getKey(c.env.DB, verified.channelId);
  if (ingestKey === null) return errorJson(c, 404, "No key found. Generate one first.");

  return c.json({ channelId: verified.channelId, ingestKey, ingestUrl: INGEST_URL });
});

channels.post("/keys", async (c) => {
  const verified = await verifyBroadcaster(c);
  if ("error" in verified) return verified.error;

  const ingestKey = await generateKey(c.env.DB, verified.channelId);
  return c.json({ channelId: verified.channelId, ingestKey, ingestUrl: INGEST_URL });
});