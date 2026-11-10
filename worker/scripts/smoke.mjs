#!/usr/bin/env node
// End-to-end smoke test for a running Truckload Worker (local `wrangler dev` or a deployed one).
// Zero dependencies. It mints Twitch-style JWTs from the extension secret, so run it with the SAME secret the
// Worker has. It writes a test channel, a key and a couple of snapshots, so point it at a test deployment.
//
//   node scripts/smoke.mjs --url https://truckload.<you>.workers.dev --secret <base64 secret> [--channel smoke-test]
//   (or set TWITCH_EXTENSION_SECRET instead of --secret)

import { createHmac } from "node:crypto";

function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i > 0 ? process.argv[i + 1] : fallback;
}

const base = (arg("url") ?? "").replace(/\/+$/, "");
const secret = arg("secret", process.env.TWITCH_EXTENSION_SECRET);
const channel = arg("channel", "smoke-test");

if (!base || !secret) {
  console.error("Usage: node scripts/smoke.mjs --url <https://...> --secret <base64 extension secret> [--channel <id>]");
  process.exit(2);
}

const b64url = (s) => Buffer.from(s).toString("base64url");
function jwt(role) {
  const data = `${b64url(JSON.stringify({ alg: "HS256", typ: "JWT" }))}.${b64url(JSON.stringify({ channel_id: channel, role, exp: Math.floor(Date.now() / 1000) + 300 }))}`;
  return `${data}.${createHmac("sha256", Buffer.from(secret, "base64")).update(data).digest("base64url")}`;
}

const payload = (job) => ({
  v: 1,
  ts: Math.floor(Date.now() / 1000),
  connected: true,
  paused: false,
  game: "ats",
  units: "imperial",
  ...(job ? { job: { active: true, cargo: "Smoke Test Steel", source: "Reno", destination: "Boise", distance: 300, etaMinutes: 200 } } : {}),
  truck: { make: "Peterbilt", model: "579", licensePlate: "SMOKE", fuelPercent: 80, damagePercent: 1, odometer: 1000 },
  dashboard: { speed: 55, signal: "off", lights: "low", wipers: false, warnings: [], restMinutes: 300 },
});

let failures = 0;
const pass = (name, extra = "") => console.log(`  PASS  ${name}${extra ? `  (${extra})` : ""}`);
const fail = (name, why) => {
  failures++;
  console.log(`  FAIL  ${name}\n        ${why}`);
};

async function call(path, init = {}) {
  const res = await fetch(base + path, init);
  const text = await res.text();
  let json;
  try {
    json = JSON.parse(text);
  } catch {
    // not JSON (landing page, images)
  }
  return { res, text, json };
}

async function check(name, fn) {
  try {
    const extra = await fn();
    pass(name, extra);
  } catch (error) {
    fail(name, error instanceof Error ? error.message : String(error));
  }
}

function expect(condition, message) {
  if (!condition) throw new Error(message);
}

console.log(`Smoke testing ${base} as channel "${channel}"\n`);

let key = "";

console.log("Static pages and health");
await check("GET /api/health is ok with a working database", async () => {
  const { res, json } = await call("/api/health");
  expect(res.status === 200 && json?.status === "ok" && json?.db === "ok", `HTTP ${res.status} ${JSON.stringify(json)}`);
  return `version ${json.version}`;
});
await check("GET /api/health/live", async () => {
  const { res } = await call("/api/health/live");
  expect(res.status === 200, `HTTP ${res.status}`);
});
await check("GET / serves the landing page", async () => {
  const { res, text } = await call("/");
  expect(res.status === 200 && text.includes("Truckload"), `HTTP ${res.status}`);
});
await check("GET /logo.png is an image", async () => {
  const { res } = await call("/logo.png");
  expect(res.status === 200 && (res.headers.get("content-type") ?? "").startsWith("image/"), `HTTP ${res.status} ${res.headers.get("content-type")}`);
});
await check("GET /about/ serves the docs site", async () => {
  const { res } = await call("/about/");
  expect(res.status === 200, `HTTP ${res.status}`);
});

console.log("\nKey management (needs the right extension secret)");
await check("GET /api/channels/keys without a token is 401", async () => {
  const { res } = await call("/api/channels/keys");
  expect(res.status === 401, `HTTP ${res.status}`);
});
await check("a viewer token is refused with 403", async () => {
  const { res } = await call("/api/channels/keys", { method: "POST", headers: { Authorization: `Bearer ${jwt("viewer")}` } });
  expect(res.status === 403, `HTTP ${res.status} (401 means the secret here differs from the Worker's)`);
});
await check("a broadcaster can generate an ingest key", async () => {
  const { res, json } = await call("/api/channels/keys", { method: "POST", headers: { Authorization: `Bearer ${jwt("broadcaster")}` } });
  expect(res.status === 200 && /^[0-9a-f]{64}$/.test(json?.ingestKey ?? ""), `HTTP ${res.status} ${JSON.stringify(json)}`);
  key = json.ingestKey;
});
await check("GET /api/channels/keys returns the same key", async () => {
  const { json } = await call("/api/channels/keys", { headers: { Authorization: `Bearer ${jwt("broadcaster")}` } });
  expect(json?.ingestKey === key, "key differs");
});

console.log("\nIngest and read-back");
const ingest = (body, k = key) => call("/api/ingest", { method: "POST", headers: { "X-Api-Key": k, "Content-Type": "application/json" }, body: JSON.stringify(body) });
await check("ingest with a wrong key is 401", async () => {
  const { res } = await ingest(payload(true), "0".repeat(64));
  expect(res.status === 401, `HTTP ${res.status}`);
});
await check("ingest with an invalid payload is 400", async () => {
  const { res, json } = await ingest({ v: 9, units: "x" });
  expect(res.status === 400 && json?.details, `HTTP ${res.status}`);
});
await check("ingest of an active job is stored", async () => {
  const { res, json } = await ingest(payload(true));
  expect(res.status === 200 && json?.stored === true, `HTTP ${res.status} ${JSON.stringify(json)}`);
  return `broadcast: ${json.broadcast}`;
});
await check("GET /api/telemetry/:channel returns it", async () => {
  const { res, json } = await call(`/api/telemetry/${channel}`);
  expect(res.status === 200 && json?.job?.cargo === "Smoke Test Steel", `HTTP ${res.status}`);
});
await check("finishing the job archives it to history", async () => {
  const { res } = await ingest(payload(false));
  expect(res.status === 200, `HTTP ${res.status}`);
  const { json } = await call(`/api/telemetry/${channel}/history?limit=5`);
  expect(Array.isArray(json) && json.some((e) => e.cargo === "Smoke Test Steel"), `history: ${JSON.stringify(json)}`);
});

console.log(failures === 0 ? "\nAll checks passed." : `\n${failures} check(s) failed.`);
process.exit(failures === 0 ? 0 : 1);