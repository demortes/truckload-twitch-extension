// Pure rules for whether a video-overlay view should be on screen. No imports, so it can be
// unit-tested directly (see visibility.test.js).

/** How long the overlay stays up after the last live telemetry before it hides itself. */
export const OFFLINE_GRACE_MS = 120_000;

const TRUCK_GAME_PATTERN = /american truck simulator|euro truck simulator/i;

/** True when a Twitch category name is ATS or ETS2. */
export function isTruckGame(game) {
  return typeof game === 'string' && TRUCK_GAME_PATTERN.test(game);
}

/**
 * @param {object} input
 * @param {boolean} input.connected       bridge says the game is running
 * @param {boolean} input.stale           no fresh telemetry for a while
 * @param {number|null} input.lastLiveAt  epoch ms of the newest live payload, null if none seen
 * @param {number} input.now              epoch ms
 * @param {string} input.game             Twitch category from onContext; '' when unknown
 * @param {boolean} input.showWhenOffline broadcaster's "always show" choice
 */
export function shouldShowOverlay({ connected, stale, lastLiveAt, now, game, showWhenOffline }) {
  // Live telemetry is the strongest signal: they are playing, whatever the category says.
  if (connected && !stale) return true;

  // A known, non-truck category means they are doing something else. Twitch often reports an empty
  // or out-of-date value, so an unknown category is ignored rather than treated as "not a truck game".
  if (game && !isTruckGame(game)) return false;

  if (showWhenOffline) return true;

  // Ride out a crash, an alt-tab or a reload for a couple of minutes, then get out of the way.
  return lastLiveAt != null && now - lastLiveAt < OFFLINE_GRACE_MS;
}
