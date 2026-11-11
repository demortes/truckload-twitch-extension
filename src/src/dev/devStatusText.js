// Pure wording for the dev status banner (see DevBanner.jsx): what the page is showing and why.

const STALE_AFTER_S = 90;
const HIDE_AFTER_S = 120; // the overlay and video component hide themselves after this long offline

function formatAge(seconds) {
  if (seconds < 60) return `${Math.max(0, Math.round(seconds))}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}m`;
  return `${Math.round(seconds / 3600)}h`;
}

function hostOf(url) {
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
}

/**
 * @param {object} input
 * @param {object|null} input.dev        resolveDevBackend() result, or null when no channel is configured
 * @param {{phase: string, error?: string|null}|null} input.status  the dev polling status
 * @param {{connected: boolean, lastTs: number|null, game: string|null}} input.telemetry
 * @param {number} input.now             epoch ms
 * @returns {{level: 'info'|'ok'|'warn'|'error', text: string}}
 */
export function describeDevStatus({ dev, status, telemetry, now }) {
  if (!dev) {
    return {
      level: 'info',
      text: 'DEV: mock data, no channel set. Add ?channel=<id> to the URL, or set VITE_DEV_CHANNEL in src/.env.development.local, to see live data from production.',
    };
  }

  const where = `${hostOf(dev.backend)}${dev.backendSource === 'default' ? ' (default)' : ''} / channel ${dev.channel}`;

  if (!status || status.phase === 'idle') return { level: 'info', text: `DEV live: ${where}: connecting...` };
  if (status.phase === 'error') return { level: 'error', text: `DEV live: ${where}: ${status.error}` };

  const ageS = telemetry.lastTs ? (now - telemetry.lastTs * 1000) / 1000 : Infinity;
  if (!Number.isFinite(ageS)) return { level: 'warn', text: `DEV live: ${where}: connected but the payload has no timestamp` };

  if (ageS > STALE_AFTER_S) {
    const hint = ageS > HIDE_AFTER_S ? ' The overlay and video component hide themselves now.' : '';
    return { level: 'warn', text: `DEV live: ${where}: last data is ${formatAge(ageS)} old (is the bridge sending?).${hint}` };
  }
  if (!telemetry.connected) return { level: 'warn', text: `DEV live: ${where}: bridge is up but the game is not running (updated ${formatAge(ageS)} ago)` };

  return { level: 'ok', text: `DEV live: ${where}: ${telemetry.game ?? 'game'} running, updated ${formatAge(ageS)} ago` };
}