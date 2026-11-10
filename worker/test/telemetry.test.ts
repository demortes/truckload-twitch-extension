import { describe, expect, it } from "vitest";
import { app } from "../src/index";
import { addChannel, createEnv } from "./helpers";

const get = (env: any, path: string) => app.fetch(new Request(`http://localhost${path}`), env);

describe("GET /api/telemetry/:channelId", () => {
  it("is 404 with the original error body when nothing is stored", async () => {
    const { env } = createEnv();
    const res = await get(env, "/api/telemetry/999");
    expect(res.status).toBe(404);
    expect(await res.json()).toEqual({ error: "No telemetry data found for this channel", details: null });
  });

  it("returns the stored payload untouched, as application/json", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const stored = '{"v":1,"ts":5,"connected":true}';
    sqlite.prepare("INSERT INTO telemetry_snapshots (channel_id, payload_json, updated_at) VALUES ('123', ?, 'now')").run(stored);

    const res = await get(env, "/api/telemetry/123");
    expect(res.status).toBe(200);
    expect(res.headers.get("Content-Type")).toContain("application/json");
    expect(await res.text()).toBe(stored);
  });

  it("needs no authentication and allows any origin", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    sqlite.prepare("INSERT INTO telemetry_snapshots (channel_id, payload_json, updated_at) VALUES ('123', '{}', 'now')").run();
    const res = await app.fetch(new Request("http://localhost/api/telemetry/123", { headers: { Origin: "https://example.com" } }), env);
    expect(res.headers.get("Access-Control-Allow-Origin")).toBe("*");
  });
});

describe("GET /api/telemetry/:channelId/history", () => {
  function seed(sqlite: any, count: number) {
    addChannel(sqlite);
    for (let i = 0; i < count; i++)
      sqlite
        .prepare("INSERT INTO job_history (id, channel_id, cargo, source, destination, distance, completed_at) VALUES (?, '123', ?, 'A', 'B', ?, ?)")
        .run(`id-${i}`, `Cargo ${i}`, i, `2026-11-01T00:00:${String(i).padStart(2, "0")}.000Z`);
  }

  it("returns an empty list for an unknown channel", async () => {
    const { env } = createEnv();
    const res = await get(env, "/api/telemetry/999/history");
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual([]);
  });

  it("returns newest first with the response field names", async () => {
    const { env, sqlite } = createEnv();
    seed(sqlite, 3);
    const rows = (await (await get(env, "/api/telemetry/123/history")).json()) as any[];
    expect(rows.map((r) => r.cargo)).toEqual(["Cargo 2", "Cargo 1", "Cargo 0"]);
    expect(rows[0]).toEqual({ id: "id-2", cargo: "Cargo 2", source: "A", destination: "B", distance: 2, completedAt: "2026-11-01T00:00:02.000Z" });
  });

  it("defaults to 20 and clamps the limit to 1..100", async () => {
    const { env, sqlite } = createEnv();
    seed(sqlite, 30);
    const count = async (query: string) => ((await (await get(env, `/api/telemetry/123/history${query}`)).json()) as any[]).length;
    expect(await count("")).toBe(20);
    expect(await count("?limit=5")).toBe(5);
    expect(await count("?limit=0")).toBe(1);
    expect(await count("?limit=-3")).toBe(1);
    expect(await count("?limit=500")).toBe(30);
    expect(await count("?limit=abc")).toBe(20);
  });
});