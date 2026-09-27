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
  }
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

## Limits (enforced by the backend, and by the bridge before sending)

- Serialized body ≤ 4096 bytes (comfortably under Twitch's 5KB PubSub message cap).
- String fields ≤ 64 characters.
- Percentages (`fuelPercent`, `damagePercent`) between 0 and 100.
- `distance`, `odometer`, `etaMinutes` ≥ 0.
- Unknown top-level fields are rejected, not silently dropped.

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
