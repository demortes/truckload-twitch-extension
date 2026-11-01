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

// True once Twitch has delivered the saved broadcaster segment (its first onChanged). Until then
// readConfig() only sees defaults, so merging a save into it would erase whatever is already stored.
let configLoaded = false;

/** Whether saved settings can be written without overwriting ones that have not loaded yet. */
export function isConfigLoaded() {
  return configLoaded;
}

/**
 * Merges `changes` into the saved config and writes it. Only callable from the config view.
 * @returns {boolean} false when nothing was written (no Twitch, or the saved config has not loaded)
 */
function writeConfig(changes) {
  if (!window.Twitch?.ext?.configuration) {
    console.warn('[Config] Twitch configuration service unavailable; cannot save.');
    return false;
  }
  if (!configLoaded) {
    console.warn('[Config] Saved configuration has not loaded yet; not saving to avoid overwriting it.');
    return false;
  }
  window.Twitch.ext.configuration.set('broadcaster', CONFIG_VERSION, JSON.stringify({ ...readConfig(), ...changes }));
  return true;
}

/** Saves the broadcaster's player ID list. Only callable from the config view. */
export function savePlayerIds(playerIds) {
  return writeConfig({ playerIds });
}

/** Saves the "keep the overlay visible when not playing" choice. Only callable from the config view. */
export function saveShowWhenOffline(showWhenOffline) {
  return writeConfig({ showWhenOffline: !!showWhenOffline });
}

// Twitch keeps a single onChanged callback (like onAuthorized), so every subscriber goes through one
// shared registration.
const configSubscribers = new Set();
let configRegistered = false;

/**
 * Calls `callback(config)` with the latest { playerIds, showWhenOffline } whenever the broadcaster
 * segment changes (including the initial load, per the Twitch Configuration Service behavior).
 * Nothing is delivered, and {@link isConfigLoaded} stays false, until that first load.
 * Returns an unsubscribe function; a no-op outside a Twitch extension context.
 */
export function onConfigChanged(callback) {
  if (!window.Twitch?.ext?.configuration) return () => {};

  configSubscribers.add(callback);
  if (!configRegistered) {
    configRegistered = true;
    window.Twitch.ext.configuration.onChanged(() => {
      configLoaded = true;
      const config = readConfig();
      [...configSubscribers].forEach((subscriber) => subscriber(config));
    });
  } else if (configLoaded) {
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
  isConfigLoaded,
  savePlayerIds,
  saveShowWhenOffline,
  onConfigChanged,
  onConfigurationChanged,
};
