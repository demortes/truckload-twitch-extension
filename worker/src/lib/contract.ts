// The v1 telemetry contract (docs/telemetry-contract.md): parsing, validation and canonical JSON.
// A port of shared/Truckload.Contracts (TelemetryPayload + TelemetryPayloadValidator), rule for rule.

export const MAX_BODY_BYTES = 4096;
const SUPPORTED_SCHEMA_VERSION = 1;
const MAX_STRING_LENGTH = 64;
const MAX_EVENTS = 5;
const MAX_SPEED = 500;
const MAX_REST_MINUTES = 7 * 24 * 60;

export const SIGNALS = ["off", "left", "right", "hazard"] as const;
export const LIGHTS = ["off", "parking", "low", "high"] as const;
export const WARNINGS = ["fuel", "oil", "coolant", "battery", "adblue", "air", "parkingBrake"] as const;
const EVENT_SEVERITIES = ["info", "warning", "critical"];

export interface JobInfo {
  active: boolean;
  cargo: string | null;
  source: string | null;
  destination: string | null;
  distance: number;
  etaMinutes: number;
}
export interface TruckInfo {
  make: string | null;
  model: string | null;
  licensePlate: string | null;
  fuelPercent: number;
  damagePercent: number;
  odometer: number;
}
export interface DashboardInfo {
  speed: number;
  signal: string | null;
  lights: string | null;
  wipers: boolean;
  warnings: string[] | null;
  restMinutes: number | null;
}
export interface TelemetryEvent {
  type: string | null;
  severity: string | null;
  message: string | null;
}
export interface TelemetryPayload {
  v: number;
  ts: number;
  connected: boolean;
  paused: boolean;
  game: string | null;
  units: string | null;
  job: JobInfo | null;
  truck: TruckInfo | null;
  events: TelemetryEvent[] | null;
  dashboard: DashboardInfo | null;
}

/** Thrown for anything System.Text.Json would reject: wrong types, unknown members, bad numbers. */
class Malformed extends Error {}

type Obj = Record<string, unknown>;

// The .NET contract rejects members it does not know (UnmappedMemberHandling.Disallow) and matches
// names case-sensitively, so an unknown or differently-cased key is malformed.
function asObject(value: unknown, keys: readonly string[]): Obj {
  if (typeof value !== "object" || value === null || Array.isArray(value)) throw new Malformed();
  for (const key of Object.keys(value)) if (!keys.includes(key)) throw new Malformed();
  return value as Obj;
}

// A missing member keeps the .NET default; an explicit null is only accepted where the type allows it.
function str(o: Obj, key: string): string | null {
  const v = o[key];
  if (v === undefined || v === null) return null;
  if (typeof v !== "string") throw new Malformed();
  return v;
}
function bool(o: Obj, key: string): boolean {
  const v = o[key];
  if (v === undefined) return false;
  if (typeof v !== "boolean") throw new Malformed();
  return v;
}
function int32(o: Obj, key: string): number {
  const v = o[key];
  if (v === undefined) return 0;
  if (typeof v !== "number" || !Number.isInteger(v) || v < -2147483648 || v > 2147483647) throw new Malformed();
  return v;
}
function optInt32(o: Obj, key: string): number | null {
  return o[key] === undefined || o[key] === null ? null : int32(o, key);
}
function int64(o: Obj, key: string): number {
  const v = o[key];
  if (v === undefined) return 0;
  if (typeof v !== "number" || !Number.isSafeInteger(v)) throw new Malformed();
  return v;
}

function parseJob(value: unknown): JobInfo | null {
  if (value === null || value === undefined) return null;
  const o = asObject(value, ["active", "cargo", "source", "destination", "distance", "etaMinutes"]);
  return {
    active: bool(o, "active"),
    cargo: str(o, "cargo"),
    source: str(o, "source"),
    destination: str(o, "destination"),
    distance: int32(o, "distance"),
    etaMinutes: int32(o, "etaMinutes"),
  };
}

function parseTruck(value: unknown): TruckInfo | null {
  if (value === null || value === undefined) return null;
  const o = asObject(value, ["make", "model", "licensePlate", "fuelPercent", "damagePercent", "odometer"]);
  return {
    make: str(o, "make"),
    model: str(o, "model"),
    licensePlate: str(o, "licensePlate"),
    fuelPercent: int32(o, "fuelPercent"),
    damagePercent: int32(o, "damagePercent"),
    odometer: int32(o, "odometer"),
  };
}

function parseDashboard(value: unknown): DashboardInfo | null {
  if (value === null || value === undefined) return null;
  const o = asObject(value, ["speed", "signal", "lights", "wipers", "warnings", "restMinutes"]);
  let warnings: string[] | null = null;
  if (o.warnings !== undefined && o.warnings !== null) {
    if (!Array.isArray(o.warnings)) throw new Malformed();
    warnings = o.warnings.map((w) => {
      if (typeof w !== "string") throw new Malformed();
      return w;
    });
  }
  return {
    speed: int32(o, "speed"),
    signal: str(o, "signal"),
    lights: str(o, "lights"),
    wipers: bool(o, "wipers"),
    warnings,
    restMinutes: optInt32(o, "restMinutes"),
  };
}

function parseEvents(value: unknown): TelemetryEvent[] | null {
  if (value === null || value === undefined) return null;
  if (!Array.isArray(value)) throw new Malformed();
  return value.map((item) => {
    const o = asObject(item, ["type", "severity", "message"]);
    return { type: str(o, "type"), severity: str(o, "severity"), message: str(o, "message") };
  });
}

export type ParseResult = { ok: true; payload: TelemetryPayload | null } | { ok: false };

/** Parses the raw request body. `ok: false` is the equivalent of a JsonException (a 400 "Invalid telemetry payload"). */
export function parsePayload(text: string): ParseResult {
  let raw: unknown;
  try {
    raw = JSON.parse(text);
  } catch {
    return { ok: false };
  }
  if (raw === null) return { ok: true, payload: null };

  try {
    const o = asObject(raw, ["v", "ts", "connected", "paused", "game", "units", "job", "truck", "events", "dashboard"]);
    return {
      ok: true,
      payload: {
        v: int32(o, "v"),
        ts: int64(o, "ts"),
        connected: bool(o, "connected"),
        paused: bool(o, "paused"),
        game: str(o, "game"),
        units: str(o, "units"),
        job: parseJob(o.job),
        truck: parseTruck(o.truck),
        events: parseEvents(o.events),
        dashboard: parseDashboard(o.dashboard),
      },
    };
  } catch (e) {
    if (e instanceof Malformed) return { ok: false };
    throw e;
  }
}

/**
 * The stored and broadcast form: camelCase, members in contract order, nulls omitted (the .NET side
 * wrote WhenWritingNull). Consumers rely on the shape, so this must stay byte-stable.
 */
export function canonicalJson(p: TelemetryPayload): string {
  const nn = <T>(v: T | null): T | undefined => (v === null ? undefined : v);
  return JSON.stringify({
    v: p.v,
    ts: p.ts,
    connected: p.connected,
    paused: p.paused,
    game: nn(p.game),
    units: nn(p.units),
    job: p.job ? {
      active: p.job.active,
      cargo: nn(p.job.cargo),
      source: nn(p.job.source),
      destination: nn(p.job.destination),
      distance: p.job.distance,
      etaMinutes: p.job.etaMinutes,
    } : undefined,
    truck: p.truck ? {
      make: nn(p.truck.make),
      model: nn(p.truck.model),
      licensePlate: nn(p.truck.licensePlate),
      fuelPercent: p.truck.fuelPercent,
      damagePercent: p.truck.damagePercent,
      odometer: p.truck.odometer,
    } : undefined,
    events: p.events?.map((e) => ({ type: nn(e.type), severity: nn(e.severity), message: nn(e.message) })),
    dashboard: p.dashboard ? {
      speed: p.dashboard.speed,
      signal: nn(p.dashboard.signal),
      lights: nn(p.dashboard.lights),
      wipers: p.dashboard.wipers,
      warnings: nn(p.dashboard.warnings),
      restMinutes: nn(p.dashboard.restMinutes),
    } : undefined,
  });
}

const tooLong = (value: string | null) => value !== null && value.length > MAX_STRING_LENGTH;

/** Returns the contract violations, empty when the payload is valid. Messages match the C# validator. */
export function validate(payload: TelemetryPayload | null): string[] {
  const errors: string[] = [];
  if (payload === null) return ["Payload is missing or could not be parsed."];

  if (payload.v !== SUPPORTED_SCHEMA_VERSION)
    errors.push(`Unsupported schema version '${payload.v}'. Expected ${SUPPORTED_SCHEMA_VERSION}.`);

  if (payload.units !== "imperial" && payload.units !== "metric")
    errors.push("'units' must be 'imperial' or 'metric'.");

  if (tooLong(payload.game)) errors.push("'game' exceeds maximum length.");

  if (payload.connected) {
    if (payload.job) validateJob(payload.job, errors);
    if (payload.truck) validateTruck(payload.truck, errors);
  }
  if (payload.events) validateEvents(payload.events, errors);
  if (payload.dashboard) validateDashboard(payload.dashboard, errors);
  return errors;
}

function checkString(field: string, value: string | null, errors: string[]) {
  if (tooLong(value)) errors.push(`'${field}' exceeds maximum length.`);
}

function validateJob(job: JobInfo, errors: string[]) {
  checkString("Cargo", job.cargo, errors);
  checkString("Source", job.source, errors);
  checkString("Destination", job.destination, errors);
  if (job.distance < 0) errors.push("'job.distance' must not be negative.");
  if (job.etaMinutes < 0) errors.push("'job.etaMinutes' must not be negative.");
}

function validateTruck(truck: TruckInfo, errors: string[]) {
  checkString("Make", truck.make, errors);
  checkString("Model", truck.model, errors);
  if (tooLong(truck.licensePlate)) errors.push("'truck.licensePlate' exceeds maximum length.");
  if (truck.fuelPercent < 0 || truck.fuelPercent > 100) errors.push("'truck.fuelPercent' must be between 0 and 100.");
  if (truck.damagePercent < 0 || truck.damagePercent > 100) errors.push("'truck.damagePercent' must be between 0 and 100.");
  if (truck.odometer < 0) errors.push("'truck.odometer' must not be negative.");
}

function validateDashboard(d: DashboardInfo, errors: string[]) {
  if (d.speed < 0 || d.speed > MAX_SPEED) errors.push(`'dashboard.speed' must be between 0 and ${MAX_SPEED}.`);
  if (d.restMinutes !== null && (d.restMinutes < 0 || d.restMinutes > MAX_REST_MINUTES))
    errors.push(`'dashboard.restMinutes' must be between 0 and ${MAX_REST_MINUTES}.`);
  if (!d.signal || !(SIGNALS as readonly string[]).includes(d.signal))
    errors.push(`'dashboard.signal' must be one of: ${SIGNALS.join(", ")}.`);
  if (!d.lights || !(LIGHTS as readonly string[]).includes(d.lights))
    errors.push(`'dashboard.lights' must be one of: ${LIGHTS.join(", ")}.`);

  if (d.warnings === null) {
    errors.push("'dashboard.warnings' must be a list (empty when there are none).");
    return;
  }
  if (d.warnings.length > WARNINGS.length)
    errors.push(`'dashboard.warnings' must not contain more than ${WARNINGS.length} entries.`);
  for (const w of d.warnings)
    if (!(WARNINGS as readonly string[]).includes(w))
      errors.push(`'dashboard.warnings[]' entries must be one of: ${WARNINGS.join(", ")}.`);
}

function validateEvents(events: TelemetryEvent[], errors: string[]) {
  if (events.length > MAX_EVENTS) {
    errors.push(`'events' must not contain more than ${MAX_EVENTS} entries.`);
    return;
  }
  for (const e of events) {
    if (e.type === null || e.type.trim() === "") errors.push("'events[].type' must not be empty.");
    checkString("events[].type", e.type, errors);
    if (e.severity === null || !EVENT_SEVERITIES.includes(e.severity))
      errors.push(`'events[].severity' must be one of: ${EVENT_SEVERITIES.join(", ")}.`);
    if (e.message === null || e.message.trim() === "") errors.push("'events[].message' must not be empty.");
    if (e.message !== null && e.message.length > 200) errors.push("'events[].message' exceeds maximum length.");
  }
}