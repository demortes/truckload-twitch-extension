# Telemetry contract (schema v1)

This is the one payload shape used everywhere: the bridge app produces it, the
backend validates/stores/re-broadcasts it verbatim, and the frontend consumes
it identically whether it arrived via the initial `/api/telemetry/{channelId}`
fetch or a live Twitch Extension PubSub message.

The canonical C# types live in `shared/Truckload.Contracts` (`TelemetryPayload`,
`JobInfo`, `TruckInfo`) and are shared by the backend and bridge projects so
they can never drift from each other.

## Shape

```json
{
  "v": 1,
  "ts": 1727400000,
  "connected": true,
  "paused": false,
  "game": "ats",
  "units": "imperial",
  "job": {
    "active": true,
    "cargo": "Electronics",
    "source": "Los Angeles",
    "destination": "Phoenix",
    "distance": 372,
    "etaMinutes": 245
  },
  "truck": {
    "make": "Peterbilt",
    "model": "579",
    "licensePlate": "TRK-4521",
    "fuelPercent": 67,
    "damagePercent": 3,
    "odometer": 124532
  },
  "events": [
    { "type": "crash", "severity": "warning", "message": "Damage jumped 20% - possible crash or major collision." }
  ]
}
```

| Field | Type | Notes |
|---|---|---|
| `v` | int | Must be `1`. The backend rejects anything else so a future v2 payload can't be silently misread. |
| `ts` | long | Unix seconds, set by the bridge. The frontend treats data older than 90s as stale and shows an offline indicator. |
| `connected` | bool | Whether the bridge could reach the local telemetry server / the game is running. |
| `paused` | bool | Whether the game is paused. |
| `game` | `"ats"` \| `"ets2"` \| `null` | `null` when not connected. |
| `units` | `"imperial"` \| `"metric"` | Distances in this payload are **already converted**; the frontend only picks the label. Rule: ATS → imperial, ETS2 → metric, overridable on the bridge with `--units`. |
| `job` | object \| `null` | `null` when disconnected or no active job. |
| `truck` | object \| `null` | `null` when disconnected. |
| `events` | array \| `null` | Optional, transient, one-off alerts (see below). Omitted/`null` on almost every tick. |

## Events (schema v1, additive)

`events` is a deliberate departure from the rest of the payload, which is a full
steady-state snapshot re-sent on every tick regardless of whether anything
changed. An in-game happening like a crash isn't state to keep re-displaying —
it's a moment in time. Modeling it as a snapshot field (e.g. `truck.justCrashed:
bool`) would force every consumer to do their own edge-detection on a value
that's true for exactly one tick; modeling it as its own always-omitted-unless-
it-happened list keeps that "did something happen" edge already resolved by
the bridge, and it's easy to ignore for any consumer that only wants the
existing steady-state stats.

- The field is optional and additive: a payload with no `events` key looks
  identical to every payload before this feature existed, so old consumers
  (and the source-generated JSON contract's `WhenWritingNull` setting) are
  unaffected.
- Each entry is `{ type, severity, message }`, all plain strings so the
  frontend needs no per-type schema to render a generic toast. `type` is a
  short machine-readable tag (currently only `"crash"`); `severity` is one of
  `"info"`, `"warning"`, `"critical"`; `message` is the human-readable text to
  show.
- The bridge detects events by comparing the current tick's mapped `truck` to
  the previous tick's (see `TelemetryEventDetector` in
  `bridge/Truckload.Bridge/Mapping/`), independent of whether that previous
  tick was actually sent.
- Implemented today: **`crash`** — a same-tick jump in `truck.damagePercent` of
  15 points or more (Funbit's wear fields creep up far slower than that under
  normal driving, so a jump this size is a reasonable "something sudden just
  happened" heuristic).
- **Deliberately not implemented**: a `"fine"`/speeding event. The Funbit
  telemetry server's `FunbitTelemetry` DTOs (`bridge/Truckload.Bridge/Funbit/`)
  expose no fines/penalty field and no current speed or posted speed limit —
  there's no real data to back that event, so it was left out rather than
  fabricated. If Funbit's API is extended with those fields (or a future
  ticket adds a source for them), this is the natural place to add a
  `"speeding"` or `"fine"` event alongside `"crash"`.
- `events` is capped at 5 entries per payload and each `message` at 200
  characters (`TelemetryPayloadValidator`), well within the existing 4096-byte
  body limit even though in practice the bridge only ever emits one event per
  tick today.

## Limits (enforced by the backend, and by the bridge before sending)

- Serialized body ≤ 4096 bytes (comfortably under Twitch's 5KB PubSub message cap).
- String fields ≤ 64 characters.
- Percentages (`fuelPercent`, `damagePercent`) between 0 and 100.
- `distance`, `odometer`, `etaMinutes` ≥ 0.
- Unknown top-level fields are rejected, not silently dropped.

## TruckTel source (default)

By default the bridge reads the [TruckTel](https://github.com/jvanstraten/TruckTel)
plugin's REST API (`/api/rest/flat/<prefix>` for `game`, `frame`, `truck`, `job`
and `trailer`, port 8080 by default). Its flat JSON uses the SCS telemetry SDK's
own key names (`truck.fuel.amount`, `job.destination.city`, `truck.wear.engine`,
...). `bridge/Truckload.Bridge/TruckTel/TruckTelMapper.cs` converts that into the
same intermediate model the Funbit source uses, so the contract mapping below
applies to both. Notes:

- Remaining time is derived as `job.delivery.time - game.time` (game minutes);
  navigation ETA comes from `truck.navigation.time` (seconds).
- "Connected" means TruckTel answered with a `game.id`; the plugin runs inside
  the game, so there is no separate "game connected" flag.

## Funbit source quirks the bridge corrects

The bridge reads a Funbit-compatible ETS2/ATS Telemetry Web Server
(`http://localhost:25555/api/ets2/telemetry` by default) and maps its raw JSON
into this contract (`bridge/Truckload.Bridge/Mapping/TelemetryMapper.cs`):

- `navigation.estimatedTime` / `job.remainingTime` are **datetimes encoding a
  timespan relative to `0001-01-01T00:00:00`**, not a duration in seconds —
  the mapper subtracts `DateTime.MinValue` to recover actual minutes.
- There is no `job.cargo` field; cargo comes from `trailer.name`.
- `truck.odometer` is in kilometers; `navigation.estimatedDistance` is in meters.
- `damagePercent` is the worst of `wearEngine`, `wearTransmission`,
  `wearCabin`, `wearChassis`, and `wearWheels`.
