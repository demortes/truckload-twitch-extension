import { Hono } from "hono";
import type { Env } from "../env";
import { errorJson } from "../lib/http";
import { getHistory, getLatest } from "../lib/store";

export const telemetry = new Hono<{ Bindings: Env }>();

const DEFAULT_LIMIT = 20;
const MAX_LIMIT = 100;

// Registered before "/:channelId" so the literal "history" segment wins, as in ASP.NET routing.
telemetry.get("/:channelId/history", async (c) => {
  const requested = Number.parseInt(c.req.query("limit") ?? "", 10);
  const take = Math.min(Math.max(Number.isFinite(requested) ? requested : DEFAULT_LIMIT, 1), MAX_LIMIT);

  return c.json(await getHistory(c.env.DB, c.req.param("channelId"), take));
});

telemetry.get("/:channelId", async (c) => {
  const payload = await getLatest(c.env.DB, c.req.param("channelId"));
  if (payload === null) return errorJson(c, 404, "No telemetry data found for this channel");

  // The stored string is already canonical JSON; pass it through untouched.
  return c.body(payload, 200, { "Content-Type": "application/json" });
});