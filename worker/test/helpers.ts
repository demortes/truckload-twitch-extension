import { createHmac } from "node:crypto";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { DatabaseSync } from "node:sqlite";
import { vi } from "vitest";
import type { Env } from "../src/env";

// A D1 stand-in over real SQLite, so the actual migration and SQL are exercised (including RETURNING
// and the single-statement broadcast throttle), not a recording fake.
class FakeStatement {
  constructor(
    private readonly db: DatabaseSync,
    readonly sql: string,
    readonly params: unknown[] = [],
  ) {}
  bind(...params: unknown[]) {
    return new FakeStatement(this.db, this.sql, params);
  }
  async first<T>(): Promise<T | null> {
    return ((this.db.prepare(this.sql).get(...(this.params as never[])) as T | undefined) ?? null) as T | null;
  }
  async all<T>() {
    return { results: this.db.prepare(this.sql).all(...(this.params as never[])) as T[], success: true, meta: {} };
  }
  async run() {
    const info = this.db.prepare(this.sql).run(...(this.params as never[]));
    return { success: true, meta: { changes: Number(info.changes) } };
  }
}

export function createDb() {
  const sqlite = new DatabaseSync(":memory:");
  sqlite.exec("PRAGMA foreign_keys = ON");
  const migrations = join(import.meta.dirname, "../migrations");
  for (const file of readdirSync(migrations).filter((name) => name.endsWith(".sql")).sort())
    sqlite.exec(readFileSync(join(migrations, file), "utf8"));

  const d1 = {
    prepare: (sql: string) => new FakeStatement(sqlite, sql),
    async batch(statements: FakeStatement[]) {
      sqlite.exec("BEGIN");
      try {
        const out = [];
        for (const s of statements) out.push(await s.all());
        sqlite.exec("COMMIT");
        return out;
      } catch (error) {
        sqlite.exec("ROLLBACK");
        throw error;
      }
    },
  };
  return { d1: d1 as unknown as D1Database, sqlite };
}

export const SECRET = Buffer.alloc(32, 7).toString("base64");

export interface TestEnv extends Env {
  ingestCalls: string[];
}

export function createEnv(overrides: Partial<Env> = {}) {
  const { d1, sqlite } = createDb();
  const ingestCalls: string[] = [];
  const env = {
    DB: d1,
    ASSETS: { fetch: async () => new Response("asset") } as unknown as Fetcher,
    INGEST_LIMITER: { limit: async ({ key }: { key: string }) => (ingestCalls.push(key), { success: true }) } as unknown as RateLimit,
    KEYS_LIMITER: { limit: async () => ({ success: true }) } as unknown as RateLimit,
    TWITCH_CLIENT_ID: "client-id",
    TWITCH_EXTENSION_SECRET: SECRET,
    ...overrides,
  } satisfies Env;
  return { env, sqlite, ingestCalls };
}

const b64url = (input: Buffer | string) => Buffer.from(input).toString("base64url");

/** An independent HS256 implementation (node:crypto), so the Worker's WebCrypto code is checked against it. */
export function makeJwt(claims: Record<string, unknown>, secretBase64 = SECRET, header: Record<string, unknown> = { alg: "HS256", typ: "JWT" }) {
  const data = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(claims))}`;
  const signature = createHmac("sha256", Buffer.from(secretBase64, "base64")).update(data).digest("base64url");
  return `${data}.${signature}`;
}

export function decodeJwt(token: string, secretBase64 = SECRET) {
  const [h, p, s] = token.split(".") as [string, string, string];
  const expected = createHmac("sha256", Buffer.from(secretBase64, "base64")).update(`${h}.${p}`).digest("base64url");
  return { signatureValid: expected === s, header: JSON.parse(Buffer.from(h, "base64url").toString()), claims: JSON.parse(Buffer.from(p, "base64url").toString()) };
}

export const nowSeconds = () => Math.floor(Date.now() / 1000);

export function broadcasterJwt(channelId = "123", extra: Record<string, unknown> = {}) {
  return makeJwt({ channel_id: channelId, role: "broadcaster", exp: nowSeconds() + 3600, ...extra });
}

/** A valid v1 payload. */
export function payload(overrides: Record<string, unknown> = {}) {
  return {
    v: 1,
    ts: nowSeconds(),
    connected: true,
    paused: false,
    game: "ats",
    units: "imperial",
    job: { active: true, cargo: "Electronics", source: "Los Angeles", destination: "Phoenix", distance: 372, etaMinutes: 245 },
    truck: { make: "Peterbilt", model: "579", licensePlate: "TRK-4521", fuelPercent: 67, damagePercent: 3, odometer: 124532 },
    dashboard: { speed: 62, signal: "off", lights: "low", wipers: true, warnings: ["battery"], restMinutes: 275 },
    ...overrides,
  };
}

export const KEY = "a".repeat(64);

export function addChannel(sqlite: DatabaseSync, channelId = "123", key = KEY) {
  sqlite.prepare("INSERT INTO channels (channel_id, ingest_key) VALUES (?, ?)").run(channelId, key);
}

export function ingestRequest(body: unknown, headers: Record<string, string> = { "X-Api-Key": KEY }) {
  return new Request("http://localhost/api/ingest", {
    method: "POST",
    headers: { "Content-Type": "application/json", ...headers },
    body: typeof body === "string" ? body : JSON.stringify(body),
  });
}

/** Stubs fetch for the Twitch PubSub call and returns the recorded requests. */
export function stubPubSub(respond: () => Response = () => new Response(null, { status: 204 })) {
  const calls: { url: string; headers: Headers; body: any }[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url: string, init: RequestInit) => {
      calls.push({ url, headers: new Headers(init.headers), body: JSON.parse(String(init.body)) });
      return respond();
    }),
  );
  return calls;
}