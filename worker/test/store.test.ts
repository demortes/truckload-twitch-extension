import { describe, expect, it } from "vitest";
import { canonicalJson, parsePayload } from "../src/lib/contract";
import { upsertTelemetry } from "../src/lib/store";
import { addChannel, createEnv, payload } from "./helpers";

function parsed() {
  const result = parsePayload(JSON.stringify(payload()));
  if (!result.ok || !result.payload) throw new Error("bad fixture");
  return result.payload;
}

describe("upsertTelemetry broadcast slot", () => {
  it("only one of two ingests in the same millisecond wins the slot", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const p = parsed();
    const json = canonicalJson(p);
    const now = 1_800_000_000_000; // the same instant for both, as happens on a fast machine

    const first = await upsertTelemetry(env.DB, "123", p, json, now, 60_000);
    const second = await upsertTelemetry(env.DB, "123", p, json, now, 60_000);

    expect(first.broadcastAcquired).toBe(true);
    expect(second.broadcastAcquired).toBe(false);
  });

  it("respects the interval and the suppression window", async () => {
    const { env, sqlite } = createEnv();
    addChannel(sqlite);
    const p = parsed();
    const json = canonicalJson(p);
    const t0 = 1_800_000_000_000;

    expect((await upsertTelemetry(env.DB, "123", p, json, t0, 1000)).broadcastAcquired).toBe(true);
    expect((await upsertTelemetry(env.DB, "123", p, json, t0 + 999, 1000)).broadcastAcquired).toBe(false);
    expect((await upsertTelemetry(env.DB, "123", p, json, t0 + 1000, 1000)).broadcastAcquired).toBe(true);

    sqlite.prepare("UPDATE telemetry_snapshots SET broadcast_suppressed_until = ?").run(t0 + 10_000);
    expect((await upsertTelemetry(env.DB, "123", p, json, t0 + 5000, 1000)).broadcastAcquired).toBe(false);
    expect((await upsertTelemetry(env.DB, "123", p, json, t0 + 10_000, 1000)).broadcastAcquired).toBe(true);
  });
});