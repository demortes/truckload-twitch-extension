// Twitch extension JWTs (HS256, signed with the extension's Base64 secret) and the PubSub broadcast.
// Replaces System.IdentityModel.Tokens.Jwt with WebCrypto; behavior follows ChannelEndpoints and
// TwitchPubSubService in backend/Truckload.Ebs.

import type { Env } from "../env";

const encoder = new TextEncoder();
const PUBSUB_URL = "https://api.twitch.tv/helix/extensions/pubsub";

// The ingest response waits for the broadcast, and the snapshot is already stored by then, so a slow or
// silent Twitch must not hold every bridge request open. A timeout surfaces as the usual "failed" outcome.
const PUBSUB_TIMEOUT_MS = 5000;

// System.IdentityModel validates lifetimes with a 5 minute clock skew by default; keep that.
const CLOCK_SKEW_SECONDS = 300;

function base64ToBytes(b64: string): Uint8Array<ArrayBuffer> {
  const bin = atob(b64);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes;
}

function bytesToBase64Url(bytes: ArrayBuffer | Uint8Array): string {
  const view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  let bin = "";
  for (const b of view) bin += String.fromCharCode(b);
  return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function base64UrlToBytes(s: string): Uint8Array<ArrayBuffer> {
  const padded = s.replace(/-/g, "+").replace(/_/g, "/") + "=".repeat((4 - (s.length % 4)) % 4);
  return base64ToBytes(padded);
}

// A bad configured secret throws here (a 500), unlike a bad token, which is just rejected.
function importKey(secretBase64: string, usage: Array<"sign" | "verify">): Promise<CryptoKey> {
  return crypto.subtle.importKey("raw", base64ToBytes(secretBase64), { name: "HMAC", hash: "SHA-256" }, false, usage);
}

export async function signJwt(secretBase64: string, claims: Record<string, unknown>): Promise<string> {
  const header = bytesToBase64Url(encoder.encode(JSON.stringify({ alg: "HS256", typ: "JWT" })));
  const payload = bytesToBase64Url(encoder.encode(JSON.stringify(claims)));
  const signature = await crypto.subtle.sign("HMAC", await importKey(secretBase64, ["sign"]), encoder.encode(`${header}.${payload}`));
  return `${header}.${payload}.${bytesToBase64Url(signature)}`;
}

export type JwtResult = { ok: true; claims: Record<string, unknown> } | { ok: false };

/** Verifies signature (HS256 only) and lifetime. Returns ok:false for any malformed, forged or expired token. */
export async function verifyJwt(secretBase64: string, token: string, nowMs: number = Date.now()): Promise<JwtResult> {
  const key = await importKey(secretBase64, ["verify"]);

  const parts = token.split(".");
  if (parts.length !== 3) return { ok: false };
  const [headerPart, payloadPart, signaturePart] = parts as [string, string, string];

  let header: unknown;
  let claims: unknown;
  let signature: Uint8Array<ArrayBuffer>;
  try {
    header = JSON.parse(new TextDecoder().decode(base64UrlToBytes(headerPart)));
    claims = JSON.parse(new TextDecoder().decode(base64UrlToBytes(payloadPart)));
    signature = base64UrlToBytes(signaturePart);
  } catch {
    return { ok: false };
  }

  if (typeof header !== "object" || header === null || (header as { alg?: unknown }).alg !== "HS256") return { ok: false };
  if (typeof claims !== "object" || claims === null || Array.isArray(claims)) return { ok: false };

  const valid = await crypto.subtle.verify("HMAC", key, signature, encoder.encode(`${headerPart}.${payloadPart}`));
  if (!valid) return { ok: false };

  const c = claims as Record<string, unknown>;
  const nowSeconds = nowMs / 1000;
  if (typeof c.exp !== "number" || nowSeconds > c.exp + CLOCK_SKEW_SECONDS) return { ok: false };
  if (typeof c.nbf === "number" && nowSeconds < c.nbf - CLOCK_SKEW_SECONDS) return { ok: false };

  return { ok: true, claims: c };
}

export type BroadcastOutcome = "sent" | "rate_limited" | "failed" | "disabled";

export interface BroadcastResult {
  outcome: BroadcastOutcome;
  /** Set for rate_limited: how long to leave the channel alone. */
  retryAfterMs?: number;
}

const DEFAULT_RETRY_AFTER_MS = 5000;

/** Relays one telemetry message to every viewer of the channel over Twitch Extension PubSub. */
export async function broadcast(env: Env, channelId: string, messageJson: string): Promise<BroadcastResult> {
  if (env.BROADCAST_ENABLED === "false") return { outcome: "disabled" };

  try {
    const jwt = await signJwt(env.TWITCH_EXTENSION_SECRET, {
      channel_id: channelId,
      role: "external",
      pubsub_perms: { send: ["broadcast"] },
      exp: Math.floor(Date.now() / 1000) + 60,
    });

    const response = await fetch(PUBSUB_URL, {
      method: "POST",
      signal: AbortSignal.timeout(PUBSUB_TIMEOUT_MS),
      headers: {
        "Client-Id": env.TWITCH_CLIENT_ID,
        Authorization: `Bearer ${jwt}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        target: ["broadcast"],
        broadcaster_id: channelId,
        is_global_broadcast: false,
        message: messageJson,
      }),
    });

    if (response.ok) return { outcome: "sent" };

    if (response.status === 429) {
      const seconds = Number.parseInt(response.headers.get("Retry-After") ?? "", 10);
      const retryAfterMs = Number.isFinite(seconds) && seconds >= 0 ? seconds * 1000 : DEFAULT_RETRY_AFTER_MS;
      console.warn(JSON.stringify({ event: "pubsub_rate_limited", channelId, retryAfterMs }));
      return { outcome: "rate_limited", retryAfterMs };
    }

    const body = (await response.text()).slice(0, 500);
    console.warn(JSON.stringify({ event: "pubsub_failed", channelId, status: response.status, body }));
    return { outcome: "failed" };
  } catch (error) {
    console.warn(JSON.stringify({ event: "pubsub_send_error", channelId, error: String(error) }));
    return { outcome: "failed" };
  }
}