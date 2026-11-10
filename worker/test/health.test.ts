import { describe, expect, it } from "vitest";
import { app } from "../src/index";
import { createEnv } from "./helpers";

const get = (env: any, path: string) => app.fetch(new Request(`http://localhost${path}`), env);

describe("health", () => {
  it("live answers without touching the database", async () => {
    const { env } = createEnv({ DB: { prepare: () => { throw new Error("must not be called"); } } as unknown as D1Database });
    const res = await get(env, "/api/health/live");
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ status: "ok", version: expect.any(String), db: "unchecked" });
  });

  it.each(["/api/health", "/api/health/ready"])("%s checks the database", async (path) => {
    const { env } = createEnv();
    const res = await get(env, path);
    expect(res.status).toBe(200);
    expect(await res.json()).toMatchObject({ status: "ok", db: "ok" });
  });

  it("reports 503 degraded when the database is unreachable", async () => {
    const { env } = createEnv({ DB: { prepare: () => ({ first: async () => { throw new Error("down"); } }) } as unknown as D1Database });
    const res = await get(env, "/api/health");
    expect(res.status).toBe(503);
    expect(await res.json()).toMatchObject({ status: "degraded", db: "error" });
  });

  it("returns a JSON 404 for unknown API paths", async () => {
    const { env } = createEnv();
    const res = await get(env, "/api/nope");
    expect(res.status).toBe(404);
    expect(await res.json()).toEqual({ error: "Not found", details: null });
  });
});