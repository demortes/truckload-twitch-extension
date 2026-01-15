## Repo Overview

This extension is a small React + Vite multi-entry web app designed for Twitch/overlay usage. The project lives in the `src/` folder and produces four independent HTML entry points used by the extension UI: `panel.html`, `overlay.html`, `mobile.html`, and `component.html`.

Key files:
- `src/package.json` — dev/build scripts ([src/package.json](src/package.json#L1-L40)).
- `src/vite.config.js` — multi-entry Vite config; inputs are the four HTML files ([src/vite.config.js](src/vite.config.js#L1-L40)).
- `src/src/services/telemetryService.js` — central client that aggregates Trucky API + local telemetry ([src/src/services/telemetryService.js](src/src/services/telemetryService.js#L1-L40)).
- `src/src/services/truckyApi.js` — wrapper for external Trucky API with an `ApiError` class ([src/src/services/truckyApi.js](src/src/services/truckyApi.js#L1-L40)).

High-level architecture
- UI: React components in `src/src/views/*` and `src/src/components/*`. Each view (panel, overlay, mobile, component) mounts its own React app and uses the same components/services.
- Services: `telemetryService` provides a single source of truth (subscribe/notify). Components call `telemetryService.subscribe()` to receive updates.
- External integrations: live game telemetry comes from a local WebSocket server (default `ws://localhost:25555/api/ets2/telemetry`) and the remote Trucky API (`https://api.truckyapp.com/v2`).

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
- Multi-entry Vite: the build output contains multiple standalone bundles. See `vite.config.js` inputs ([src/vite.config.js](src/vite.config.js#L1-L40)). Don’t assume a single SPA route.
- Two-level `src` layout: repository root contains a `src/` folder which itself contains another `src/` (JS sources). When referencing files in code or in scripts, use the inner `src/src/...` paths for source code and `src/...` for build tools.
- Telemetry: live data is guarded by `USE_LIVE_TELEMETRY` in `PanelApp.jsx` for development; components use mocked data unless you explicitly enable live telemetry ([src/src/views/panel/PanelApp.jsx](src/src/views/panel/PanelApp.jsx#L1-L80)).
- Local WebSocket dependency: `telemetryService` will try to connect to `ws://localhost:25555` by default. If you do not have a local telemetry server (ETS2/ATS telemetry bridge or Trucky desktop), keep `USE_LIVE_TELEMETRY=false` when developing.
- External API calls: `truckyApi.js` adds a `User-Agent` header; it throws `ApiError` on non-2xx responses — tests/components expect this shape ([src/src/services/truckyApi.js](src/src/services/truckyApi.js#L1-L40)).

> Example: to run the panel locally with live telemetry enabled, change `USE_LIVE_TELEMETRY` to `true` in [src/src/views/panel/PanelApp.jsx](src/src/views/panel/PanelApp.jsx#L1-L40) and start the dev server.

Patterns for code edits and PRs
- Prefer updating `telemetryService` when adding cross-view data needs — it centralizes polling, socket handling, and listener management.
- Keep components pure and read-only: components call `subscribe()` and render state; avoid starting/stopping polling directly inside presentational components.

Debugging tips
- Console logs: `telemetryService` and `truckyApi` log connection/fetch issues to the browser console — check the browser devtools for errors during `npm run dev`.
- WebSocket problems: ensure the telemetry server is reachable and CORS/socket URL matches `telemetryService` `localTelemetryUrl` or override via `initialize()`.

If something is missing
- Tell me which area you want expanded (build details, telemetry adapter examples, or component conventions) and I will update this file.

---
Please review and tell me if any runtime steps or integrations are inaccurate or if you want short snippets for common tasks (e.g., mocking telemetry during tests).
