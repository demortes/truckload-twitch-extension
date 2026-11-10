import { describe, expect, it } from "vitest";
import { canonicalJson, parsePayload, validate } from "../src/lib/contract";
import { payload } from "./helpers";

function parse(body: unknown) {
  const result = parsePayload(JSON.stringify(body));
  if (!result.ok) throw new Error("expected the payload to parse");
  return result.payload;
}

describe("parsePayload", () => {
  it("accepts a valid payload and reports no errors", () => {
    expect(validate(parse(payload()))).toEqual([]);
  });

  it("rejects members the contract does not know, like the .NET Disallow setting", () => {
    expect(parsePayload(JSON.stringify({ ...payload(), surprise: 1 })).ok).toBe(false);
    expect(parsePayload(JSON.stringify(payload({ truck: { make: "x", extra: 1 } }))).ok).toBe(false);
  });

  it("matches member names case-sensitively", () => {
    expect(parsePayload(JSON.stringify({ ...payload(), Units: "imperial" })).ok).toBe(false);
  });

  it("rejects wrong types and non-integer numbers", () => {
    expect(parsePayload(JSON.stringify(payload({ ts: "now" }))).ok).toBe(false);
    expect(parsePayload(JSON.stringify(payload({ connected: "yes" }))).ok).toBe(false);
    expect(parsePayload(JSON.stringify(payload({ truck: { ...payload().truck, fuelPercent: 12.5 } }))).ok).toBe(false);
    expect(parsePayload(JSON.stringify(payload({ job: { ...payload().job, distance: 3e10 } }))).ok).toBe(false);
  });

  it("rejects text that is not JSON, and treats a literal null as a missing payload", () => {
    expect(parsePayload("{nope").ok).toBe(false);
    const result = parsePayload("null");
    expect(result).toEqual({ ok: true, payload: null });
    expect(validate(null)).toEqual(["Payload is missing or could not be parsed."]);
  });

  it("defaults missing members the way System.Text.Json does", () => {
    const parsed = parse({ units: "metric" });
    expect(parsed).toMatchObject({ v: 0, ts: 0, connected: false, paused: false, game: null, job: null });
    expect(validate(parsed)).toEqual(["Unsupported schema version '0'. Expected 1."]);
  });
});

describe("validate", () => {
  it("checks the schema version and units", () => {
    expect(validate(parse(payload({ v: 2 })))).toEqual(["Unsupported schema version '2'. Expected 1."]);
    expect(validate(parse(payload({ units: "furlongs" })))).toEqual(["'units' must be 'imperial' or 'metric'."]);
  });

  it("limits string lengths", () => {
    const long = "x".repeat(65);
    expect(validate(parse(payload({ game: long })))).toContain("'game' exceeds maximum length.");
    expect(validate(parse(payload({ job: { ...payload().job, cargo: long } })))).toContain("'Cargo' exceeds maximum length.");
  });

  it("range-checks the truck and job, but only while connected", () => {
    const bad = payload({ truck: { ...payload().truck, fuelPercent: 101, odometer: -1 }, job: { ...payload().job, distance: -5 } });
    expect(validate(parse(bad))).toEqual([
      "'job.distance' must not be negative.",
      "'truck.fuelPercent' must be between 0 and 100.",
      "'truck.odometer' must not be negative.",
    ]);
    expect(validate(parse({ ...bad, connected: false }))).toEqual([]);
  });

  it("validates the dashboard vocabularies and ranges", () => {
    const bad = payload({ dashboard: { speed: 501, signal: "wiggle", lights: "disco", wipers: false, warnings: ["fuel", "nope"], restMinutes: -1 } });
    const errors = validate(parse(bad));
    expect(errors).toContain("'dashboard.speed' must be between 0 and 500.");
    expect(errors).toContain("'dashboard.restMinutes' must be between 0 and 10080.");
    expect(errors).toContain("'dashboard.signal' must be one of: off, left, right, hazard.");
    expect(errors).toContain("'dashboard.lights' must be one of: off, parking, low, high.");
    expect(errors).toContain("'dashboard.warnings[]' entries must be one of: fuel, oil, coolant, battery, adblue, air, parkingBrake.");
  });

  it("requires a warnings list and caps its length", () => {
    expect(validate(parse(payload({ dashboard: { speed: 1, signal: "off", lights: "off", wipers: false } })))).toEqual([
      "'dashboard.warnings' must be a list (empty when there are none).",
    ]);
    const eight = ["fuel", "oil", "coolant", "battery", "adblue", "air", "parkingBrake", "fuel"];
    expect(validate(parse(payload({ dashboard: { speed: 1, signal: "off", lights: "off", wipers: false, warnings: eight } })))).toContain(
      "'dashboard.warnings' must not contain more than 7 entries.",
    );
  });

  it("validates events", () => {
    const ok = { type: "crash", severity: "critical", message: "Crashed" };
    expect(validate(parse(payload({ events: [ok] })))).toEqual([]);
    expect(validate(parse(payload({ events: [{ type: "", severity: "loud", message: "" }] })))).toEqual([
      "'events[].type' must not be empty.",
      "'events[].severity' must be one of: info, warning, critical.",
      "'events[].message' must not be empty.",
    ]);
    expect(validate(parse(payload({ events: Array(6).fill(ok) })))).toEqual(["'events' must not contain more than 5 entries."]);
  });
});

describe("canonicalJson", () => {
  it("writes camelCase members in contract order and omits nulls", () => {
    const parsed = parse({ v: 1, ts: 5, connected: false, paused: false, units: "metric", truck: { make: "Scania", fuelPercent: 10 } });
    expect(canonicalJson(parsed!)).toBe(
      '{"v":1,"ts":5,"connected":false,"paused":false,"units":"metric","truck":{"make":"Scania","fuelPercent":10,"damagePercent":0,"odometer":0}}',
    );
  });

  it("keeps an empty events list and empty warnings", () => {
    const parsed = parse(payload({ events: [], dashboard: { speed: 0, signal: "off", lights: "off", wipers: false, warnings: [] } }));
    const out = JSON.parse(canonicalJson(parsed!));
    expect(out.events).toEqual([]);
    expect(out.dashboard.warnings).toEqual([]);
    expect(out.dashboard).not.toHaveProperty("restMinutes");
  });

  it("round-trips: parsing the canonical form gives the same canonical form", () => {
    const once = canonicalJson(parse(payload())!);
    const reparsed = parsePayload(once);
    expect(reparsed.ok && canonicalJson(reparsed.payload!)).toBe(once);
  });
});