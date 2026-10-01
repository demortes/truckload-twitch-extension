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

## 3. Run the installer (Windows)

Download `TruckloadSetup-v<version>.exe` from the
[latest release](https://github.com/demortes/truckload-twitch-extension/releases/latest)
and run it. It:

1. asks which games you play (American Truck Simulator, Euro Truck Simulator 2,
   or both; games it finds on Steam are ticked for you) and confirms each
   game's folder,
2. installs the [TruckTel](https://github.com/jvanstraten/TruckTel) telemetry
   plugin into each game you ticked (run the installer again later to add the
   other game),
3. asks for your ingest key from step 2 and saves it, and
4. installs the Truckload Bridge with a Start menu / desktop shortcut.

Windows may show a "Windows protected your PC" (SmartScreen) prompt because the
installer isn't code-signed yet; choose **More info, then Run anyway**. If Setup
says it can't write to a game folder, run it again as administrator
(right-click, *Run as administrator*).

To start streaming: launch the game, then open **Truckload Bridge** from the
shortcut. It keeps a console window open while it sends data; close it to stop.

### Manual install (Linux, macOS, or if you prefer)

- Unzip `trucktel.zip` from TruckTel's
  [releases](https://github.com/jvanstraten/TruckTel/releases) into
  `<game>/bin/win_x64/plugins` (`bin/linux_x64/plugins` on Linux).
- Download the Bridge zip for your OS from the latest release, then run:

```
Truckload.Bridge.exe --ingest-url https://<backend-domain>/api/ingest --key <your-key> --save
```

`--save` remembers this so future runs need no arguments. Already use Funbit's
telemetry server? Add `--source funbit`.

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
