// Dev-only escape hatch for looking at a real channel's telemetry without Twitch.
//
//   http://localhost:5173/overlay.html?channel=<twitch channel id>&backend=https://truckload.demortes.com
//
// The extension normally only learns its channel id from Twitch's onAuthorized, which never fires in a
// plain browser tab. `import.meta.env.DEV` is false in production builds, so this always returns null
// there and the whole feature is dead code that ships nowhere.
export function getDevBackend() {
  if (!import.meta.env.DEV) return null;

  const params = new URLSearchParams(window.location.search);
  const channel = params.get('channel');
  if (!channel || !/^[\w-]+$/.test(channel)) return null;

  const backend = (params.get('backend') || import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080').replace(/\/+$/, '');
  if (!/^https?:\/\//.test(backend)) return null;

  return { channel, backend };
}
