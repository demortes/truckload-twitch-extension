/**
 * Convoy Configuration
 *
 * Reads and writes the broadcaster's list of TruckersMP/Trucky player IDs
 * using Twitch's Extension Configuration Service (broadcaster segment). This
 * is the standard idiom for small, per-channel settings like this one: it
 * needs no new backend endpoint or database table, and every viewer's
 * extension instance can read the same value the broadcaster saved from the
 * config view.
 */

const CONFIG_VERSION = '1';

/**
 * Parses the raw broadcaster segment content into a player ID array. Defaults
 * to [] for anything missing, malformed, or shaped unexpectedly.
 */
export function parsePlayerIds(content) {
  if (!content) return [];
  try {
    const parsed = JSON.parse(content);
    return Array.isArray(parsed?.playerIds)
      ? parsed.playerIds.filter((id) => typeof id === 'string' && id.length > 0)
      : [];
  } catch {
    return [];
  }
}

/** Reads the currently configured player IDs, if any. */
export function getConfiguredPlayerIds() {
  return parsePlayerIds(window.Twitch?.ext?.configuration?.broadcaster?.content);
}

/** Saves the broadcaster's player ID list. Only callable from the config view. */
export function savePlayerIds(playerIds) {
  if (!window.Twitch?.ext?.configuration) {
    console.warn('[ConvoyConfig] Twitch configuration service unavailable; cannot save.');
    return;
  }
  window.Twitch.ext.configuration.set('broadcaster', CONFIG_VERSION, JSON.stringify({ playerIds }));
}

/**
 * Subscribes to configuration changes, invoking `callback` with the latest
 * player IDs whenever the broadcaster segment changes (including the initial
 * load, per the Twitch Configuration Service behavior).
 */
export function onConfigurationChanged(callback) {
  if (!window.Twitch?.ext?.configuration) return;
  window.Twitch.ext.configuration.onChanged(() => callback(getConfiguredPlayerIds()));
}

export default {
  parsePlayerIds,
  getConfiguredPlayerIds,
  savePlayerIds,
  onConfigurationChanged,
};
