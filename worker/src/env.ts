/** Bindings, secrets and variables the Worker reads. Keep this, wrangler.example.jsonc and the README in step. */
export interface Env {
  DB: D1Database;
  ASSETS: Fetcher;
  INGEST_LIMITER: RateLimit;
  KEYS_LIMITER: RateLimit;

  // Secrets
  TWITCH_CLIENT_ID: string;
  TWITCH_EXTENSION_SECRET: string; // Base64

  // Optional variables
  BROADCAST_ENABLED?: string;
  MIN_BROADCAST_INTERVAL_MS?: string;
}