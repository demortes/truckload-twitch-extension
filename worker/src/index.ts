import { Hono } from "hono";
import { cors } from "hono/cors";
import type { Env } from "./env";
import { channels } from "./routes/channels";
import { health } from "./routes/health";
import { ingest } from "./routes/ingest";
import { telemetry } from "./routes/telemetry";

export const app = new Hono<{ Bindings: Env }>();

// Twitch extensions run in iframes on many domains, so any origin may call the API. The credentials
// are the ingest key (X-Api-Key) or the extension JWT (Authorization), never cookies.
app.use(
  "/api/*",
  cors({ origin: "*", allowMethods: ["GET", "POST"], allowHeaders: ["Authorization", "Content-Type", "X-Api-Key"] }),
);

app.route("/api/health", health);
app.route("/api/ingest", ingest);
app.route("/api/telemetry", telemetry);
app.route("/api/channels", channels);

app.notFound((c) => c.json({ error: "Not found", details: null }, 404));

app.onError((error, c) => {
  console.error(JSON.stringify({ event: "unhandled_error", path: c.req.path, error: String(error) }));
  return c.json({ error: "Internal server error", details: null }, 500);
});

// Everything that is not /api/* is served straight from static assets by the platform (see
// assets.run_worker_first in wrangler.jsonc), so the Worker only ever sees API paths.
export default { fetch: app.fetch } satisfies ExportedHandler<Env>;