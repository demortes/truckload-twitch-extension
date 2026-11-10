import { afterEach, describe, expect, it, vi } from "vitest";
import { app } from "../src/index";
import { KEY, SECRET, addChannel, createEnv, decodeJwt, ingestRequest, payload, stubPubSub } from "./helpers";

afterEach(() => vi.unstubAllGlobals());

async function ingest(env: any, body: unknown, headers?: Record<string, string>) {
  const res = await app.fetch(ingestRequest(body, headers), env);
  return { res, json: (await res.json()) as any };
}

describe("POST /api/ingest: authentication and limits", () => {
  it("rejects a missing key with 401", async () => {
    const { env } = createEnv();
    const { res, json } = await ingest(env, payload(), {});
    expect(res.status).toBe(401);
    expect(json).toEqual({ error: "Missing API key", details: null });
  });

  it("rejects an unknown key with 401", async () => {
    const { env } = createEnv();
    const { res, json } = await ingest(env, payload(), { "X-Api-Key": "b".repeat(64) });
    expect(res.status).toBe(401);
    expect(json.error).toBe("Invalid API key");
  });

  it("does not accept the key in the query string", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const res = await app.fetch(new Request(`http://localhost/api/ingest?key=${KEY}`, { method: "POST", body: JSON.stringify(payload()) }), env);
    expect(res.status).toBe(401);
  });

  it("returns 429 with Retry-After when the rate limiter says no", async () => {
    const { env, sqlite } = createEnv({ INGEST_LIMITER: { limit: async () => ({ success: false }) } as unknown as RateLimit });
    addChannel(sqlite);
    const { res, json } = await ingest(env, payload());
    expect(res.status).toBe(429);
    expect(res.headers.get("Retry-After")).toBe("1");
    expect(json.error).toMatch(/Too many requests/);
  });

  it("gives well-formed keys their own limiter bucket and shares one for everything else", async () => {
    const { env, sqlite, ingestCalls } = createEnv();
    addChannel(sqlite);
    stubPubSub();
    await ingest(env, payload());
    await ingest(env, payload(), { "X-Api-Key": "not-a-real-key" });
    await ingest(env, payload(), { "X-Api-Key": "also-bad" });
    expect(ingestCalls).toEqual([KEY, "malformed", "malformed"]);
  });

  it("rejects a declared oversize body with 413 before reading it", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const { res, json } = await ingest(env, "x".repeat(5000));
    expect(res.status).toBe(413);
    expect(json.error).toBe("Request body exceeds 4096 bytes");
  });

  it("rejects an undeclared oversize body with 413 while streaming", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const stream = new ReadableStream({
      start(controller) {
        for (let i = 0; i < 10; i++) controller.enqueue(new TextEncoder().encode("y".repeat(1000)));
        controller.close();
      },
    });
    const req = new Request("http://localhost/api/ingest", { method: "POST", headers: { "X-Api-Key": KEY }, body: stream, duplex: "half" } as RequestInit);
    const res = await app.fetch(req, env);
    expect(res.status).toBe(413);
  });

  it("rejects an empty body with 400", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const { res, json } = await ingest(env, "");
    expect(res.status).toBe(400);
    expect(json.error).toBe("Empty request body");
  });
});

describe("POST /api/ingest: payload checks", () => {
  it("rejects malformed JSON with a generic 400 that does not echo the body", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const { res, json } = await ingest(env, "{not json secret-fragment");
    expect(res.status).toBe(400);
    expect(json).toEqual({ error: "Invalid telemetry payload", details: null });
  });

  it("rejects unknown members as an invalid payload", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const { res, json } = await ingest(env, { ...payload(), extra: true });
    expect(res.status).toBe(400);
    expect(json.error).toBe("Invalid telemetry payload");
  });

  it("returns validation errors in details", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const { res, json } = await ingest(env, payload({ units: "furlongs", v: 9 }));
    expect(res.status).toBe(400);
    expect(json.error).toBe("Telemetry payload failed validation");
    expect(json.details).toBe("Unsupported schema version '9'. Expected 1. 'units' must be 'imperial' or 'metric'.");
  });

  it("treats a literal null body as failing validation", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const { res, json } = await ingest(env, "null");
    expect(res.status).toBe(400);
    expect(json.details).toBe("Payload is missing or could not be parsed.");
  });
});

describe("POST /api/ingest: store and broadcast", () => {
  it("stores the canonical payload and broadcasts it over Twitch PubSub", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const calls = stubPubSub();

    const { res, json } = await ingest(env, payload());
    expect(res.status).toBe(200);
    expect(json).toEqual({ stored: true, broadcast: "sent", retryAfterMs: null });

    const stored = sqlite.prepare("SELECT payload_json FROM telemetry_snapshots WHERE channel_id = '123'").get() as { payload_json: string };
    expect(JSON.parse(stored.payload_json)).toMatchObject({ v: 1, connected: true, units: "imperial" });

    expect(calls).toHaveLength(1);
    const call = calls[0]!;
    expect(call.url).toBe("https://api.twitch.tv/helix/extensions/pubsub");
    expect(call.headers.get("Client-Id")).toBe("client-id");
    expect(call.body).toEqual({ target: ["broadcast"], broadcaster_id: "123", is_global_broadcast: false, message: stored.payload_json });

    const jwt = decodeJwt(call.headers.get("Authorization")!.replace("Bearer ", ""), SECRET);
    expect(jwt.signatureValid).toBe(true);
    expect(jwt.header).toMatchObject({ alg: "HS256" });
    expect(jwt.claims).toMatchObject({ channel_id: "123", role: "external", pubsub_perms: { send: ["broadcast"] } });
    expect(jwt.claims.exp - Math.floor(Date.now() / 1000)).toBeLessThanOrEqual(60);
  });

  it("throttles a second ingest inside the minimum interval but still stores it", async () => {
    const { env, sqlite } = createEnv({ MIN_BROADCAST_INTERVAL_MS: "60000" });
    addChannel(sqlite);
    const calls = stubPubSub();

    expect((await ingest(env, payload())).json.broadcast).toBe("sent");
    const second = await ingest(env, payload({ dashboard: { speed: 70, signal: "off", lights: "low", wipers: true, warnings: [] } }));
    expect(second.json).toEqual({ stored: true, broadcast: "throttled" });
    expect(calls).toHaveLength(1);

    const stored = sqlite.prepare("SELECT payload_json FROM telemetry_snapshots WHERE channel_id = '123'").get() as { payload_json: string };
    expect(JSON.parse(stored.payload_json).dashboard.speed).toBe(70);
  });

  it("broadcasts again once the interval has passed", async () => {
    const { env, sqlite } = createEnv({ MIN_BROADCAST_INTERVAL_MS: "1000" });
    addChannel(sqlite);
    const calls = stubPubSub();

    await ingest(env, payload());
    sqlite.prepare("UPDATE telemetry_snapshots SET last_broadcast_at = last_broadcast_at - 5000").run();
    expect((await ingest(env, payload())).json.broadcast).toBe("sent");
    expect(calls).toHaveLength(2);
  });

  it("backs off a channel that Twitch rate limits, using Retry-After", async () => {
    const { env, sqlite } = createEnv({ MIN_BROADCAST_INTERVAL_MS: "1" });
    addChannel(sqlite);
    const calls = stubPubSub(() => new Response("slow down", { status: 429, headers: { "Retry-After": "30" } }));

    const first = await ingest(env, payload());
    expect(first.json).toEqual({ stored: true, broadcast: "rate_limited", retryAfterMs: 30000 });

    await new Promise((r) => setTimeout(r, 5));
    const second = await ingest(env, payload());
    expect(second.json.broadcast).toBe("throttled");
    expect(calls).toHaveLength(1);
  });

  it("defaults the back-off to 5 seconds when Twitch sends no Retry-After", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    stubPubSub(() => new Response(null, { status: 429 }));
    expect((await ingest(env, payload())).json.retryAfterMs).toBe(5000);
  });

  it("reports failed when Twitch errors, and when the call throws", async () => {
    const { env, sqlite } = createEnv({ MIN_BROADCAST_INTERVAL_MS: "0" });
    addChannel(sqlite);
    stubPubSub(() => new Response("nope", { status: 500 }));
    expect((await ingest(env, payload())).json.broadcast).toBe("failed");

    vi.stubGlobal("fetch", vi.fn(async () => { throw new Error("network down"); }));
    expect((await ingest(env, payload())).json.broadcast).toBe("failed");
  });

  it("stores but never calls Twitch when broadcasting is disabled", async () => {
    const { env, sqlite } = createEnv({ BROADCAST_ENABLED: "false" });
    addChannel(sqlite);
    const calls = stubPubSub();
    const { res, json } = await ingest(env, payload());
    expect(res.status).toBe(200);
    expect(json.broadcast).toBe("disabled");
    expect(calls).toHaveLength(0);
    expect(sqlite.prepare("SELECT COUNT(*) AS n FROM telemetry_snapshots").get()).toEqual({ n: 1 });
  });
});

describe("POST /api/ingest: completed job history", () => {
  const withJob = (cargo: string) => payload({ job: { active: true, cargo, source: "A", destination: "B", distance: 10, etaMinutes: 5 } });
  const noJob = () => payload({ job: undefined });
  const history = (sqlite: any) => sqlite.prepare("SELECT cargo, source, destination, distance FROM job_history ORDER BY completed_at").all();

  it("archives the previous job when an active job ends", async () => {
    const { env, sqlite } = createEnv({ BROADCAST_ENABLED: "false" });
    addChannel(sqlite);
    await ingest(env, withJob("Steel"));
    expect(history(sqlite)).toHaveLength(0);

    await ingest(env, noJob());
    expect(history(sqlite)).toEqual([{ cargo: "Steel", source: "A", destination: "B", distance: 10 }]);
  });

  it("does not archive while a job is still active, even a different one", async () => {
    const { env, sqlite } = createEnv({ BROADCAST_ENABLED: "false" });
    addChannel(sqlite);
    await ingest(env, withJob("Steel"));
    await ingest(env, withJob("Coal"));
    expect(history(sqlite)).toHaveLength(0);
  });

  it("does not archive twice for one job", async () => {
    const { env, sqlite } = createEnv({ BROADCAST_ENABLED: "false" });
    addChannel(sqlite);
    await ingest(env, withJob("Steel"));
    await ingest(env, noJob());
    await ingest(env, noJob());
    expect(history(sqlite)).toHaveLength(1);
  });

  it("keeps only the 20 most recent jobs per channel", async () => {
    const { env, sqlite } = createEnv({ BROADCAST_ENABLED: "false" });
    addChannel(sqlite);
    for (let i = 0; i < 23; i++) {
      await ingest(env, withJob(`Cargo ${i}`));
      await ingest(env, noJob());
      await new Promise((r) => setTimeout(r, 2));
    }
    const rows = history(sqlite);
    expect(rows).toHaveLength(20);
    expect(rows[0].cargo).toBe("Cargo 3");
    expect(rows[19].cargo).toBe("Cargo 22");
  });
});

describe("POST /api/ingest: a stuck Twitch", () => {
  it("gives the PubSub call an abort signal and reports failed when it times out", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    let seenSignal: AbortSignal | null | undefined;
    vi.stubGlobal(
      "fetch",
      vi.fn(async (_url: string, init: RequestInit) => {
        seenSignal = init.signal;
        // Behave like a Twitch that never answers: only the abort signal can end this call.
        return new Promise<Response>((_, reject) => {
          init.signal?.addEventListener("abort", () => reject(init.signal?.reason ?? new Error("aborted")));
        });
      }),
    );
    const timeout = vi.spyOn(AbortSignal, "timeout").mockImplementation(() => {
      const controller = new AbortController();
      setTimeout(() => controller.abort(new DOMException("timed out", "TimeoutError")), 10);
      return controller.signal;
    });

    const started = Date.now();
    const { res, json } = await ingest(env, payload());

    expect(timeout).toHaveBeenCalledWith(5000);
    expect(seenSignal).toBeDefined();
    expect(res.status).toBe(200);
    expect(json).toEqual({ stored: true, broadcast: "failed", retryAfterMs: null });
    expect(Date.now() - started).toBeLessThan(2000);
    timeout.mockRestore();
  });
});