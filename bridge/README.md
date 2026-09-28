# Truckload Bridge

A small console app that reads live ATS/ETS2 telemetry from a local
[Funbit-compatible telemetry server](https://github.com/Funbit/ets2-telemetry-server)
and sends it to your Truckload Twitch extension backend, so your viewers see
your current job and truck stats live. It's released as a self-contained
build for Windows, Linux and macOS — no separate .NET install needed on any
of them.

## Requirements

- Windows 10/11, Linux (native or via Proton), or macOS.
- The ETS2/ATS Telemetry SDK plugin and a telemetry web server running locally
  (default `http://localhost:25555/api/ets2/telemetry`). Most telemetry
  overlays for these games use this same server under the hood, regardless of
  OS.
- An ingest URL and API key from the extension's **Config** page on your
  Twitch channel dashboard.

## Quick start

Download the zip for your OS from the [releases page](../../releases) and
extract it, then run the bridge from a terminal (or double-click it on
Windows).

### Windows

```
Truckload.Bridge.exe --ingest-url https://your-backend-domain/api/ingest --key <your-key>
```

Add `--save` the first time to write those values to `truckload-bridge.json`
next to the exe, so future runs need no arguments:

```
Truckload.Bridge.exe --ingest-url https://your-backend-domain/api/ingest --key <your-key> --save
Truckload.Bridge.exe
```

### Linux

ATS/ETS2 on Linux (native or via Proton) exposes the same local telemetry
server, so the bridge works the same way — just run the Linux binary instead
of the `.exe`. You may need to mark it executable after extracting the zip:

```
chmod +x Truckload.Bridge
./Truckload.Bridge --ingest-url https://your-backend-domain/api/ingest --key <your-key>
```

Add `--save` the first time to persist those values to `truckload-bridge.json`
next to the binary:

```
./Truckload.Bridge --ingest-url https://your-backend-domain/api/ingest --key <your-key> --save
./Truckload.Bridge
```

### macOS

Same as Linux — mark the binary executable and run it from a terminal. The
first launch may require allowing it in **System Settings → Privacy &
Security**, since it isn't notarized/signed.

```
chmod +x Truckload.Bridge
./Truckload.Bridge --ingest-url https://your-backend-domain/api/ingest --key <your-key> --save
./Truckload.Bridge
```

Press `Ctrl+C` to stop, on any OS.

## Options

Run `Truckload.Bridge --help` (or `Truckload.Bridge.exe --help` on Windows)
for the full list. The important ones:

| Option | Purpose |
|---|---|
| `--ingest-url` | Backend ingest endpoint |
| `--key` | Ingest API key from the Config page |
| `--telemetry-url` | Local telemetry server (default `http://localhost:25555/api/ets2/telemetry`) |
| `--poll-ms` | Poll interval; minimum and default 1000ms |
| `--heartbeat-s` | Send a keep-alive even if nothing changed (default 30s) |
| `--units auto\|imperial\|metric` | Distance units; `auto` picks imperial for ATS, metric for ETS2 |
| `--demo [--demo-game ats\|ets2]` | Run a scripted sample job with no game required |
| `--dry-run` | Print payloads instead of sending them |
| `--save` | Persist the resolved options to `truckload-bridge.json` |
| `--verbose` | Log every payload considered, not just ones sent |

Configuration is resolved in this order (later wins): defaults →
`truckload-bridge.json` next to the exe → environment variables
(`TRUCKLOAD_INGEST_URL`, `TRUCKLOAD_INGEST_KEY`, `TRUCKLOAD_TELEMETRY_URL`) →
command-line arguments.

## Demo mode

`--demo` runs a scripted, deterministic job (no telemetry server needed) —
useful for testing your Twitch panel/overlay locally, and for Twitch
reviewers who need to see live data without installing the game:

```
Truckload.Bridge.exe --demo --ingest-url https://your-backend-domain/api/ingest --key <your-key>
```

(`./Truckload.Bridge --demo ...` on Linux/macOS.)

## Troubleshooting

- **"The backend rejected the ingest key"** — the key was regenerated or is
  wrong; the bridge exits (code 2). Get the current key from the Config page
  and re-run (or edit `truckload-bridge.json`).
- **"Could not reach the telemetry server"** — start ATS/ETS2 with the
  telemetry SDK plugin and your telemetry web server running, or pass
  `--telemetry-url` if it's on a non-default port.
- The bridge never sends more than once a second, and keeps a connection
  alive with a heartbeat every 30 seconds even while your telemetry doesn't
  change, so viewers can tell you're still streaming.
