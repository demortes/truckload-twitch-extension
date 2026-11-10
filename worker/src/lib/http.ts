import type { Context } from "hono";
import type { Env } from "../env";

export type AppContext = Context<{ Bindings: Env }>;

/** The ErrorResponse shape from the .NET API: both members always present, details null when unused. */
export function errorJson(c: AppContext, status: 400 | 401 | 403 | 404 | 413 | 429 | 503, error: string, details: string | null = null) {
  return c.json({ error, details }, status);
}

const MALFORMED_KEY_PARTITION = "malformed";

/**
 * A key shaped like a real one (64 hex characters) gets its own rate limit bucket; anything else shares
 * one, so a caller spraying random strings can neither create unbounded buckets nor dodge the limit.
 */
export function partitionKeyFor(apiKey: string | undefined | null): string {
  return apiKey && /^[0-9a-fA-F]{64}$/.test(apiKey) ? apiKey : MALFORMED_KEY_PARTITION;
}

export type BodyResult = { ok: true; bytes: Uint8Array } | { ok: false; tooLarge: true };

/** Reads at most `maxBytes` of the body, stopping early (and discarding the rest) if there is more. */
export async function readBodyLimited(request: Request, maxBytes: number): Promise<BodyResult> {
  if (!request.body) return { ok: true, bytes: new Uint8Array(0) };

  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > maxBytes) {
      await reader.cancel();
      return { ok: false, tooLarge: true };
    }
    chunks.push(value);
  }

  const bytes = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return { ok: true, bytes };
}