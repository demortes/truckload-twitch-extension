import { describe, expect, it } from "vitest";
import { app } from "../src/index";
import { SECRET, addChannel, broadcasterJwt, createEnv, makeJwt, nowSeconds } from "./helpers";

function request(method: string, token?: string) {
  return new Request("http://localhost/api/channels/keys", { method, headers: token ? { Authorization: `Bearer ${token}` } : {} });
}

async function call(env: any, method: string, token?: string) {
  const res = await app.fetch(request(method, token), env);
  return { res, json: (await res.json()) as any };
}

describe("/api/channels/keys authentication", () => {
  it("requires an Authorization header", async () => {
    const { env } = createEnv();
    const { res, json } = await call(env, "GET");
    expect(res.status).toBe(401);
    expect(json).toEqual({ error: "Missing Authorization header", details: null });
  });

  it("requires the Bearer scheme", async () => {
    const { env } = createEnv();
    const res = await app.fetch(new Request("http://localhost/api/channels/keys", { headers: { Authorization: `Basic ${broadcasterJwt()}` } }), env);
    expect(res.status).toBe(401);
  });

  it("rejects garbage, a forged signature, a wrong secret and alg none", async () => {
    const { env } = createEnv();
    const claims = { channel_id: "123", role: "broadcaster", exp: nowSeconds() + 3600 };
    const otherSecret = Buffer.alloc(32, 9).toString("base64");
    const forged = broadcasterJwt().split(".").slice(0, 2).join(".") + ".AAAA";
    const algNone = `${Buffer.from('{"alg":"none"}').toString("base64url")}.${Buffer.from(JSON.stringify(claims)).toString("base64url")}.`;

    for (const token of ["not-a-jwt", forged, makeJwt(claims, otherSecret), algNone, makeJwt(claims, SECRET, { alg: "HS512" })]) {
      const { res, json } = await call(env, "GET", token);
      expect(res.status).toBe(401);
      expect(json.error).toBe("Invalid or expired token");
    }
  });

  it("rejects an expired token but tolerates the 5 minute clock skew", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const expired = await call(env, "GET", broadcasterJwt("123", { exp: nowSeconds() - 600 }));
    expect(expired.res.status).toBe(401);

    const skewed = await call(env, "GET", broadcasterJwt("123", { exp: nowSeconds() - 60 }));
    expect(skewed.res.status).toBe(200);
  });

  it("requires an exp claim", async () => {
    const { env } = createEnv();
    const { res } = await call(env, "GET", makeJwt({ channel_id: "123", role: "broadcaster" }));
    expect(res.status).toBe(401);
  });

  it("rejects a token without a channel_id", async () => {
    const { env } = createEnv();
    const { res, json } = await call(env, "GET", makeJwt({ role: "broadcaster", exp: nowSeconds() + 60 }));
    expect(res.status).toBe(401);
    expect(json.error).toBe("Invalid token: missing channel_id");
  });

  it("forbids anyone who is not the broadcaster", async () => {
    const { env } = createEnv();
    for (const role of ["viewer", "moderator", "external"]) {
      const { res, json } = await call(env, "POST", makeJwt({ channel_id: "123", role, exp: nowSeconds() + 60 }));
      expect(res.status).toBe(403);
      expect(json.error).toBe("Only broadcasters can manage keys");
    }
  });

  it("returns 429 when the key management limiter says no", async () => {
    const { env } = createEnv({ KEYS_LIMITER: { limit: async () => ({ success: false }) } as unknown as RateLimit });
    const { res } = await call(env, "POST", broadcasterJwt());
    expect(res.status).toBe(429);
    expect(res.headers.get("Retry-After")).toBe("1");
  });
});

describe("/api/channels/keys behavior", () => {
  it("GET is 404 until a key has been generated", async () => {
    const { env } = createEnv();
    const { res, json } = await call(env, "GET", broadcasterJwt());
    expect(res.status).toBe(404);
    expect(json.error).toBe("No key found. Generate one first.");
  });

  it("POST generates a 64 character lowercase hex key and GET returns the same one", async () => {
    const { env } = createEnv();
    const created = await call(env, "POST", broadcasterJwt("555"));
    expect(created.res.status).toBe(200);
    expect(created.json).toEqual({ channelId: "555", ingestKey: expect.stringMatching(/^[0-9a-f]{64}$/), ingestUrl: "/api/ingest" });

    const fetched = await call(env, "GET", broadcasterJwt("555"));
    expect(fetched.json).toEqual(created.json);
  });

  it("regenerating replaces the key, so the old one stops working", async () => {
    const { env, sqlite } = createEnv();
    const first = (await call(env, "POST", broadcasterJwt("555"))).json.ingestKey;
    const second = (await call(env, "POST", broadcasterJwt("555"))).json.ingestKey;
    expect(second).not.toBe(first);
    expect(sqlite.prepare("SELECT COUNT(*) AS n FROM channels WHERE ingest_key = ?").get(first)).toEqual({ n: 0 });
    expect(sqlite.prepare("SELECT COUNT(*) AS n FROM channels").get()).toEqual({ n: 1 });
  });

  it("keeps each broadcaster's key separate", async () => {
    const { env } = createEnv();
    const a = (await call(env, "POST", broadcasterJwt("1"))).json.ingestKey;
    const b = (await call(env, "POST", broadcasterJwt("2"))).json.ingestKey;
    expect(a).not.toBe(b);
    expect((await call(env, "GET", broadcasterJwt("1"))).json.ingestKey).toBe(a);
  });

  it("answers CORS preflight for the headers the extension sends", async () => {
    const { env } = createEnv();
    const res = await app.fetch(
      new Request("http://localhost/api/channels/keys", {
        method: "OPTIONS",
        headers: { Origin: "https://abc.ext-twitch.tv", "Access-Control-Request-Method": "POST", "Access-Control-Request-Headers": "authorization,content-type" },
      }),
      env,
    );
    expect(res.headers.get("Access-Control-Allow-Origin")).toBe("*");
    expect(res.headers.get("Access-Control-Allow-Headers")?.toLowerCase()).toContain("authorization");
  });
});