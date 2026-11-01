/**
 * Broadcaster Configuration
 *
 * Reads and writes the broadcaster's per-channel settings (the TruckersMP/Trucky convoy player IDs
 * and whether to keep the overlay up while not playing) using Twitch's Extension Configuration
 * Service (broadcaster segment). This is the standard idiom for small, per-channel settings: it
 * needs no new backend endpoint or database table, and every viewer's extension instance can read
 * the same value the broadcaster saved from the config view.
 *
 * (The file keeps its original name from when convoy was the only setting.)
 */

const CONFIG_VERSION = '1';

/**
 * Parses the raw broadcaster segment content into { playerIds, showWhenOffline }. Anything missing,
 * malformed, or shaped unexpectedly falls back to the defaults ([] and false). Both settings live in
 * the same segment, so every writer must go through writeConfig() to avoid clobbering the other.
 */
export function parseConfig(content) {
  const defaults = { playerIds: [], showWhenOffline: false };
  if (!content) return defaults;
  try {
    const parsed = JSON.parse(content);
    return {
      playerIds: Array.isArray(parsed?.playerIds)
        ? parsed.playerIds.filter((id) => typeof id === 'string' && id.length > 0)
        : [],
      showWhenOffline: parsed?.showWhenOffline === true,
    };
  } catch {
    return defaults;
  }
}

/** Parses the raw broadcaster segment content into a player ID array. */
export function parsePlayerIds(content) {
  return parseConfig(content).playerIds;
}

function readConfig() {
  return parseConfig(window.Twitch?.ext?.configuration?.broadcaster?.content);
}

/** Reads the currently configured player IDs, if any. */
export function getConfiguredPlayerIds() {
  return readConfig().playerIds;
}

/** Whether the broadcaster wants the overlay kept on screen while they are not playing. */
export function getShowWhenOffline() {
  return readConfig().showWhenOffline;
}

/** Merges `changes` into the saved config and writes it. Only callable from the config view. */
function writeConfig(changes) {
  if (!window.Twitch?.ext?.configuration) {
    console.warn('[Config] Twitch configuration service unavailable; cannot save.');
    return;
  }
  window.Twitch.ext.configuration.set('broadcaster', CONFIG_VERSION, JSON.stringify({ ...readConfig(), ...changes }));
}

/** Saves the broadcaster's player ID list. Only callable from the config view. */
export function savePlayerIds(playerIds) {
  writeConfig({ playerIds });
}

/** Saves the "keep the overlay visible when not playing" choice. Only callable from the config view. */
export function saveShowWhenOffline(showWhenOffline) {
  writeConfig({ showWhenOffline: !!showWhenOffline });
}

// Twitch keeps a single onChanged callback (like onAuthorized), so every subscriber goes through one
// shared registration.
const configSubscribers = new Set();
let configRegistered = false;

/**
 * Calls `callback(config)` with the latest { playerIds, showWhenOffline } whenever the broadcaster
 * segment changes (including the initial load, per the Twitch Configuration Service behavior).
 * Returns an unsubscribe function; a no-op outside a Twitch extension context.
 */
export function onConfigChanged(callback) {
  if (!window.Twitch?.ext?.configuration) return () => {};

  configSubscribers.add(callback);
  if (!configRegistered) {
    configRegistered = true;
    window.Twitch.ext.configuration.onChanged(() => {
      const config = readConfig();
      [...configSubscribers].forEach((subscriber) => subscriber(config));
    });
  } else {
    callback(readConfig());
  }

  return () => {
    configSubscribers.delete(callback);
  };
}

/** Player-ID-only view of {@link onConfigChanged}, kept for the convoy code. */
export function onConfigurationChanged(callback) {
  return onConfigChanged((config) => callback(config.playerIds));
}

export default {
  parseConfig,
  parsePlayerIds,
  getConfiguredPlayerIds,
  getShowWhenOffline,
  savePlayerIds,
  saveShowWhenOffline,
  onConfigChanged,
  onConfigurationChanged,
};
