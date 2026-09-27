# Streamer setup

## 1. Install the extension

Install Truckload from the Twitch Extensions directory (or, before release,
add it to your channel from Hosted/Local Test) and activate the Panel and/or
Video Overlay component.

## 2. Generate your ingest key

Open the extension's **Config** page from your Creator Dashboard. Click
**Generate API Key**. You'll get an ingest URL and a key — keep the key
secret, since anyone with it can send data that appears on your channel's
overlay.

## 3. Download and run the Bridge

The Bridge is a small Windows app that reads live telemetry from your game and
sends it to Truckload. Requirements:

- Windows 10/11.
- The ETS2/ATS Telemetry SDK plugin and a compatible telemetry web server
  running (most telemetry overlays for these games already include this).

Download `Truckload.Bridge.exe` from the
[latest release](https://github.com/demortes/truckload-twitch-extension/releases/latest),
then run the command shown on the Config page:

```
Truckload.Bridge.exe --ingest-url https://<backend-domain>/api/ingest --key <your-key> --save
```

`--save` remembers this so future runs need no arguments — just double-click
the exe.

## 4. Verify

Start ATS or ETS2 with an active job. Your Config page's **Status** section
should show "Receiving data" within a few seconds. Your panel/overlay should
show the same job and truck stats to viewers.

## Troubleshooting

- **Config page shows no data** — make sure the Bridge is running and its
  console shows it's sending (not stuck retrying). See `bridge/README.md`.
- **"Invalid ingest key"** — you may have regenerated the key on the Config
  page after starting the Bridge. Re-run the command shown there, or edit
  `truckload-bridge.json` next to the exe.
- **Distances look wrong** — check the `--units` the Bridge is using; `auto`
  (the default) picks imperial for ATS and metric for ETS2.
