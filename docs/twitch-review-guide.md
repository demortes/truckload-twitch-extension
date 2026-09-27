# Twitch review walkthrough

This is the walkthrough to include with a Twitch extension review submission,
so the reviewer can see the extension working without installing ATS/ETS2
themselves.

## What each view shows

- **Panel** — collapsible sections for the current job (cargo, source,
  destination, distance, ETA) and truck (make/model, plate, fuel, damage,
  odometer), plus a live/offline status badge.
- **Video Overlay** — the same data laid out for an in-video overlay.
- **Video Component** — a compact collapsed icon that expands to show
  destination/ETA and truck/fuel.
- **Mobile** — the same data in a tabbed layout for the mobile app.
- **Config** (broadcaster-only) — generate/rotate the ingest API key, get the
  exact Bridge command to run, and see a live "last data received" status.

## How to see it live

1. On the test channel, open the Config page and generate a key.
2. Run the Bridge app in demo mode against the production backend, using that
   key — no game required:

   ```
   Truckload.Bridge.exe --demo --ingest-url https://<backend-domain>/api/ingest --key <key>
   ```

   This sends a scripted job that progresses over a few minutes and repeats,
   so there's always live data during review.
3. Open the Panel/Overlay/Mobile/Component views on the test channel — they
   should update roughly every second while the demo job is running, or every
   30 seconds via heartbeat if nothing changed.

## Data handling

The backend stores only a channel ID, an ingest key, and the latest telemetry
snapshot per channel (job/truck stats — no personal or viewer data). See
`docs/telemetry-contract.md` for the exact schema.
