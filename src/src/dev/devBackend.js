// Dev-only escape hatch for looking at a real channel's telemetry without Twitch.
//
//   http://localhost:5173/overlay.html?channel=<twitch channel id>
//   http://localhost:5173/overlay.html?channel=<id>&backend=http://localhost:8787     (a local Worker)
//
// Or set it once in src/.env.development.local (gitignored) and every view opened from `npm run dev` is live:
//
//   VITE_DEV_CHANNEL=<twitch channel id>
//   VITE_DEV_BACKEND=<optional; defaults to production>
//
// The extension normally only learns its channel id from Twitch's onAuthorized, which never fires in a
// plain browser tab. `import.meta.env.DEV` is false in production builds, so this always returns null
// there; the resolver and the status banner are dropped from the bundle, and the small polling function in
// the telemetry service stays but is never called.
import { resolveDevBackend } from './devConfig';

export function getDevBackend() {
  // Checked here, on the build-time constant, so production builds drop the resolver below entirely.
  if (!import.meta.env.DEV) return null;

  return resolveDevBackend({
    isDev: import.meta.env.DEV,
    search: window.location.search,
    env: import.meta.env,
  });
}