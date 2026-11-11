// A viewer's own display preferences, kept in their browser (localStorage), never sent anywhere.
//
// Twitch's Configuration Service has no per-viewer segment, and syncing preferences across devices would mean
// storing viewer ids on the backend (a privacy-disclosure change), so this is deliberately browser-local.
// Panel, overlay and video component are served from the same extension origin, so they share it and follow
// each other live via the `storage` event (see hooks/useViewerSettings.js). The mobile app is a separate device.

export const STORAGE_KEY = 'truckload.viewerSettings.v1';

const UNITS = ['auto', 'imperial', 'metric'];
const LAYOUTS = ['standard', 'compact'];
const DELIVERY_TIMES = ['ago', '12h', '24h'];
const SECTION_KEYS = ['job', 'truck', 'history', 'convoy'];

export const DEFAULT_SETTINGS = Object.freeze({
  units: 'auto',
  layout: 'standard',
  deliveryTimes: 'ago',
  sections: Object.freeze({ job: true, truck: true, history: true, convoy: true }),
});

const isPlainObject = (value) => typeof value === 'object' && value !== null && !Array.isArray(value);

/** Validates field by field: a bad value keeps the base value, unknown keys are dropped. */
function normalize(candidate, base) {
  const input = isPlainObject(candidate) ? candidate : {};
  const sections = isPlainObject(input.sections) ? input.sections : {};
  return {
    units: UNITS.includes(input.units) ? input.units : base.units,
    layout: LAYOUTS.includes(input.layout) ? input.layout : base.layout,
    deliveryTimes: DELIVERY_TIMES.includes(input.deliveryTimes) ? input.deliveryTimes : base.deliveryTimes,
    sections: Object.fromEntries(SECTION_KEYS.map((key) => [key, typeof sections[key] === 'boolean' ? sections[key] : base.sections[key]])),
  };
}

/** Parses what was saved. Anything missing, malformed or hand-edited falls back to the defaults. */
export function parseSettings(raw) {
  if (!raw) return normalize({}, DEFAULT_SETTINGS);
  try {
    return normalize(JSON.parse(raw), DEFAULT_SETTINGS);
  } catch {
    return normalize({}, DEFAULT_SETTINGS);
  }
}

const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/**
 * @param {{getItem: Function, setItem: Function} | null} storage localStorage, or null when it is unavailable
 *   (the settings then last for the session only). Storage errors never reach the caller.
 */
export function createSettingsStore(storage) {
  const listeners = new Set();

  function load() {
    try {
      return parseSettings(storage?.getItem(STORAGE_KEY));
    } catch {
      return normalize({}, DEFAULT_SETTINGS);
    }
  }

  let current = load();

  function commit(next) {
    if (same(next, current)) return;
    current = next;
    listeners.forEach((listener) => listener());
  }

  return {
    /** The same object until something actually changes, as useSyncExternalStore requires. */
    get: () => current,

    /** Merges a partial change (sections merge key by key), validates it, saves it and notifies. */
    set(patch) {
      const next = normalize({ ...current, ...patch, sections: { ...current.sections, ...patch?.sections } }, current);
      if (same(next, current)) return;
      try {
        storage?.setItem(STORAGE_KEY, JSON.stringify(next));
      } catch {
        // Blocked or full: keep the choice for this session.
      }
      commit(next);
    },

    /** Re-reads storage, for a change made by another view of the same extension. */
    reload: () => commit(load()),

    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

function browserStorage() {
  try {
    const storage = window.localStorage;
    const probe = '__truckload_probe__';
    storage.setItem(probe, '1');
    storage.removeItem(probe);
    return storage;
  } catch {
    return null; // also the case under node (no window), e.g. in unit tests
  }
}

export const viewerSettingsStore = createSettingsStore(browserStorage());