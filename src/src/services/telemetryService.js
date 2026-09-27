/**
 * Telemetry Service
 *
 * A small subscribe/notify store for the canonical v1 telemetry payload
 * (see docs/telemetry-contract.md). Both of the ways this data reaches the
 * frontend — the initial-state fetch from the backend and live updates over
 * Twitch Extension PubSub — funnel through the same applyPayload(), so every
 * view sees identical, correctly-shaped data regardless of source.
 *
 * The bridge app (bridge/Truckload.Bridge) is what talks to the local game
 * telemetry server and Twitch PubSub broadcast; this service only consumes
 * what the backend hands it.
 */

import { FEATURES } from '../config/features';
import truckyApi from './truckyApi';

const STALE_AFTER_MS = 90_000;
const STALE_CHECK_INTERVAL_MS = 5_000;

let state = {
  connected: false,
  stale: false,
  game: null,
  units: 'imperial',
  job: null,
  truck: null,
  convoy: null,
  gameTime: null,
  lastTs: null,
};

let listeners = [];
let initialized = false;
let listening = false;
let staleTimer = null;
let truckyPollTimer = null;

export function subscribe(callback) {
  listeners.push(callback);
  callback(state);
  return () => {
    listeners = listeners.filter((l) => l !== callback);
  };
}

function notifyListeners() {
  listeners.forEach((callback) => callback(state));
}

function updateState(updates) {
  state = { ...state, ...updates };
  notifyListeners();
}

export function getState() {
  return { ...state };
}

/**
 * Applies a canonical v1 telemetry payload to the store. This is the single
 * entry point used by both the initial-state fetch and the PubSub listener,
 * so the two paths can never disagree about the shape of the data.
 */
function applyPayload(payload) {
  if (!payload || payload.v !== 1) {
    console.warn('[Telemetry] Ignoring payload with unexpected shape:', payload);
    return;
  }

  updateState({
    connected: !!payload.connected,
    stale: false,
    game: payload.game ?? null,
    units: payload.units === 'metric' ? 'metric' : 'imperial',
    job: payload.job ?? null,
    truck: payload.truck ?? null,
    lastTs: payload.ts ?? Math.floor(Date.now() / 1000),
  });
}

function startStaleTracking() {
  stopStaleTracking();
  staleTimer = setInterval(() => {
    if (state.lastTs == null) return;
    const ageMs = Date.now() - state.lastTs * 1000;
    const stale = ageMs > STALE_AFTER_MS;
    if (stale !== state.stale) {
      updateState({ stale, connected: stale ? false : state.connected });
    }
  }, STALE_CHECK_INTERVAL_MS);
}

function stopStaleTracking() {
  if (staleTimer) {
    clearInterval(staleTimer);
    staleTimer = null;
  }
}

async function fetchInitialState(backendUrl, channelId) {
  try {
    const response = await fetch(`${backendUrl}/api/telemetry/${channelId}`);
    if (response.ok) {
      applyPayload(await response.json());
    }
  } catch (err) {
    console.warn('[Telemetry] Failed to fetch initial state:', err);
  }
}

function handlePubSubMessage(_target, _contentType, message) {
  try {
    applyPayload(JSON.parse(message));
  } catch (err) {
    console.error('[Telemetry] Failed to parse PubSub message:', err);
  }
}

export function connectTwitchPubSub() {
  if (!window.Twitch?.ext) {
    console.warn('[Telemetry] Twitch Extension Helper not found. Skipping PubSub.');
    return;
  }
  if (listening) return;

  console.log('[Telemetry] Listening for Twitch PubSub messages...');
  window.Twitch.ext.listen('broadcast', handlePubSubMessage);
  listening = true;
}

/**
 * Initialize the telemetry service. Safe to call more than once (e.g. Twitch
 * can call onAuthorized again on token refresh): re-fetches the latest state,
 * but never registers a second PubSub listener.
 */
export function initialize(options = {}) {
  if (options.backendUrl && options.channelId) {
    fetchInitialState(options.backendUrl, options.channelId);
  }

  if (window.Twitch?.ext) {
    connectTwitchPubSub();
  }

  if (FEATURES.convoy && options.playerIds) {
    startTruckyPolling(options.playerIds);
  }

  if (!initialized) {
    startStaleTracking();
    initialized = true;
  }
}

export function cleanup() {
  stopStaleTracking();
  stopTruckyPolling();
  if (window.Twitch?.ext && listening) {
    window.Twitch.ext.unlisten('broadcast', handlePubSubMessage);
    listening = false;
  }
  initialized = false;
  listeners = [];
}

// =============================================================================
// Trucky API integration (convoy) — kept for a future release; gated by
// FEATURES.convoy and never invoked while that flag is off.
// =============================================================================

export function startTruckyPolling(playerIds = []) {
  stopTruckyPolling();

  const poll = async () => {
    try {
      await fetchTruckyData(playerIds);
    } catch (err) {
      console.error('[Telemetry] Trucky API poll failed:', err);
    }
  };

  poll();
  truckyPollTimer = setInterval(poll, 30_000);
}

export function stopTruckyPolling() {
  if (truckyPollTimer) {
    clearInterval(truckyPollTimer);
    truckyPollTimer = null;
  }
}

async function fetchTruckyData(playerIds) {
  try {
    const timeData = await truckyApi.getGameTime();
    updateState({ gameTime: timeData });
  } catch (err) {
    console.warn('[Telemetry] Failed to fetch game time:', err);
  }

  if (playerIds.length > 0) {
    const members = await Promise.all(
      playerIds.map(async (id) => {
        try {
          const status = await truckyApi.getPlayerOnlineStatus(id);
          return {
            id,
            name: status.name || `Player ${id}`,
            isOnline: status.online || false,
            isLeader: id === playerIds[0],
          };
        } catch {
          return { id, name: `Player ${id}`, isOnline: false, isLeader: id === playerIds[0] };
        }
      })
    );

    const onlineMembers = members.filter((m) => m.isOnline);
    updateState({ convoy: { active: onlineMembers.length > 0, members: onlineMembers } });
  }
}

export default {
  subscribe,
  initialize,
  cleanup,
  getState,
  connectTwitchPubSub,
  startTruckyPolling,
  stopTruckyPolling,
};
