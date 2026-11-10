import { Hono } from "hono";
import type { Env } from "../env";
import type { AppContext } from "../lib/http";

export const health = new Hono<{ Bindings: Env }>();

// Bump when the API surface changes; it is reported by every health endpoint.
const VERSION = "2.0.0";

async function canReachDatabase(db: D1Database): Promise<boolean> {
  try {
    const timeout = new Promise<never>((_, reject) => setTimeout(() => reject(new Error("timeout")), 2000));
    await Promise.race([db.prepare("SELECT 1").first(), timeout]);
    return true;
  } catch {
    return false;
  }
}

// Liveness: the Worker is up. Deliberately does not touch the database.
health.get("/live", (c) => c.json({ status: "ok", version: VERSION, db: "unchecked" }));

// Readiness (also served at /api/health, as before): includes a database check.
async function ready(c: AppContext) {
  const dbOk = await canReachDatabase(c.env.DB);
  return c.json({ status: dbOk ? "ok" : "degraded", version: VERSION, db: dbOk ? "ok" : "error" }, dbOk ? 200 : 503);
}
health.get("/ready", ready);
health.get("/", ready);