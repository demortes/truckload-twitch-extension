## Repo Overview

This extension is a small React + Vite multi-entry web app designed for Twitch/overlay usage. The project lives in the `src/` folder and produces five independent HTML entry points used by the extension UI: `panel.html`, `overlay.html`, `mobile.html`, `component.html`, and `config.html`.

Key files:
- `src/package.json` — dev/build scripts ([src/package.json](src/package.json#L1-L40)).
- `src/vite.config.js` — multi-entry Vite config; inputs are the five HTML files, `base: './'` so built assets work when Twitch hosts them ([src/vite.config.js](src/vite.config.js#L1-L40)).
- `src/src/services/telemetryService.js` — the subscribe/notify telemetry store. It consumes the canonical v1 payload (see `docs/telemetry-contract.md`) identically from the backend's initial-state fetch and from Twitch Extension PubSub ([src/src/services/telemetryService.js](src/src/services/telemetryService.js#L1-L60)).
- `src/src/hooks/useTelemetry.js` — the shared hook every view uses to wire up Twitch auth + the telemetry store; falls back to `src/src/dev/mockTelemetry.js` only when there's no `window.Twitch.ext` (plain local dev).
- `src/src/config/features.js` — feature flags; `convoy` is off in v1.
- `src/src/services/truckyApi.js` — wrapper for the external Trucky API (currently only used by the convoy feature, which is disabled) ([src/src/services/truckyApi.js](src/src/services/truckyApi.js#L1-L40)).

High-level architecture
- UI: React components in `src/src/views/*` and `src/src/components/*`. Each view (panel, overlay, mobile, component, config) mounts its own React app and uses the same components/services.
- Services: `telemetryService` provides a single source of truth (subscribe/notify). Components call `telemetryService.subscribe()` (or, more commonly, the `useTelemetry()` hook) to receive updates.
- External integrations: live game telemetry does **not** come from the frontend anymore. The `bridge/Truckload.Bridge` app (a separate .NET console app, not part of `src/`) reads the local ETS2/ATS telemetry server and POSTs the canonical payload to the backend's `/api/ingest`. The backend relays it over Twitch Extension PubSub and the frontend only ever consumes the canonical shape.

Developer workflows
- Start dev server (serves all HTML entries):

  ```bash
  cd src
  npm install
  npm run dev
  ```

- Build production bundle:

  ```bash
  cd src
  npm run build
  ```

- Linting:

  ```bash
  cd src
  npm run lint
  ```

Project-specific conventions and gotchas
- Multi-entry Vite: the build output contains multiple standalone bundles. See `vite.config.js` inputs ([src/vite.config.js](src/vite.config.js#L1-L40)). Don't assume a single SPA route.
- Two-level `src` layout: repository root contains a `src/` folder which itself contains another `src/` (JS sources). When referencing files in code or in scripts, use the inner `src/src/...` paths for source code and `src/...` for build tools.
- Telemetry is live by default. Without `window.Twitch.ext` present (e.g. `npm run dev` opened directly, no Twitch helper context), every view falls back to the shared mock data in `src/src/dev/mockTelemetry.js` — there is no `USE_LIVE_TELEMETRY` flag anymore.
- The old local-WebSocket telemetry path (`ws://localhost:25555`) has been removed from the frontend entirely; that logic now lives in `bridge/Truckload.Bridge/Mapping/TelemetryMapper.cs`.
- External API calls: `truckyApi.js` adds a `User-Agent` header; it throws `ApiError` on non-2xx responses — tests/components expect this shape ([src/src/services/truckyApi.js](src/src/services/truckyApi.js#L1-L40)). It's currently unused while `FEATURES.convoy` is off.

Patterns for code edits and PRs
- Prefer updating `telemetryService`/`useTelemetry` when adding cross-view data needs — they centralize the PubSub listener and store.
- Keep components pure and read-only: components call `subscribe()` (via the hook) and render state; avoid starting/stopping polling directly inside presentational components.
- Any change to the wire payload shape must be made in `shared/Truckload.Contracts` (the canonical contract, used by both the backend and the bridge), then mirrored here.

Debugging tips
- Console logs: `telemetryService` logs connection/parse issues to the browser console — check devtools during `npm run dev`.
- To see live data locally without Twitch, run `bridge/Truckload.Bridge --demo` against a local backend, and load a view with a small dev-only Twitch helper shim (see the streamer/dev setup doc for the exact env vars).

If something is missing
- Tell me which area you want expanded (build details, telemetry adapter examples, or component conventions) and I will update this file.

---
Please review and tell me if any runtime steps or integrations are inaccurate or if you want short snippets for common tasks (e.g., mocking telemetry during tests).
