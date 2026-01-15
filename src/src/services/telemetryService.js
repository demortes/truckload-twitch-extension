/**
 * Telemetry Service
 *
 * Aggregates data from multiple sources:
 * - Trucky API: TruckersMP online status, convoy members, traffic
 * - Local Telemetry: Job info, truck stats (via WebSocket from local telemetry server)
 *
 * For local telemetry, this service expects a WebSocket connection to a local
 * telemetry server (e.g., ETS2 Telemetry Server, Trucky desktop app, or custom solution)
 * that reads from the SCS Telemetry SDK.
 */

import truckyApi from './truckyApi';

// =============================================================================
// Configuration
// =============================================================================

const CONFIG = {
  // Local telemetry WebSocket (adjust based on your telemetry server)
  localTelemetryUrl: 'ws://localhost:25555/api/ets2/telemetry',

  // Polling intervals (ms)
  truckyPollInterval: 30000, // 30 seconds for TruckersMP data
  localPollInterval: 1000,   // 1 second for local telemetry

  // Game type
  game: 'ats', // 'ats' or 'ets2'
};

// =============================================================================
// State
// =============================================================================

let state = {
  connected: false,
  game: null,
  job: null,
  truck: null,
  convoy: null,
  gameTime: null,
};

let listeners = [];
let localSocket = null;
let truckyPollTimer = null;

// =============================================================================
// Event System
// =============================================================================

/**
 * Subscribe to telemetry updates
 * @param {function} callback - Called with updated state
 * @returns {function} Unsubscribe function
 */
export function subscribe(callback) {
  listeners.push(callback);
  // Immediately call with current state
  callback(state);

  return () => {
    listeners = listeners.filter(l => l !== callback);
  };
}

/**
 * Notify all listeners of state change
 */
function notifyListeners() {
  listeners.forEach(callback => callback(state));
}

/**
 * Update state and notify listeners
 */
function updateState(updates) {
  state = { ...state, ...updates };
  notifyListeners();
}

// =============================================================================
// Local Telemetry (WebSocket)
// =============================================================================

/**
 * Connect to local telemetry server
 * @param {string} url - WebSocket URL (optional, uses config default)
 */
export function connectLocalTelemetry(url = CONFIG.localTelemetryUrl) {
  if (localSocket) {
    localSocket.close();
  }

  try {
    localSocket = new WebSocket(url);

    localSocket.onopen = () => {
      console.log('[Telemetry] Connected to local telemetry server');
      updateState({ connected: true });
    };

    localSocket.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        handleLocalTelemetry(data);
      } catch (err) {
        console.error('[Telemetry] Failed to parse telemetry data:', err);
      }
    };

    localSocket.onclose = () => {
      console.log('[Telemetry] Disconnected from local telemetry server');
      updateState({ connected: false });
    };

    localSocket.onerror = (error) => {
      console.error('[Telemetry] WebSocket error:', error);
    };
  } catch (err) {
    console.error('[Telemetry] Failed to connect:', err);
  }
}

/**
 * Process incoming local telemetry data
 * Maps raw telemetry to our component format
 */
function handleLocalTelemetry(raw) {
  // Map job data
  const job = raw.job ? {
    active: raw.job.income > 0,
    cargo: raw.job.cargo || 'Unknown',
    source: raw.job.sourceCity || raw.job.source || 'Unknown',
    destination: raw.job.destinationCity || raw.job.destination || 'Unknown',
    distance: Math.round(raw.navigation?.estimatedDistance / 1609.34) || 0, // meters to miles
    etaMinutes: Math.round((raw.navigation?.estimatedTime || 0) / 60),
  } : null;

  // Map truck data
  const truck = raw.truck ? {
    make: raw.truck.make || 'Unknown',
    model: raw.truck.model || 'Unknown',
    licensePlate: raw.truck.licensePlate || null,
    fuelPercent: Math.round((raw.truck.fuel / raw.truck.fuelCapacity) * 100) || 0,
    damagePercent: Math.round((raw.truck.wearChassis || 0) * 100),
    odometer: Math.round((raw.truck.odometer || 0) / 1609.34), // km to miles
  } : null;

  // Detect game type
  const game = raw.game?.id || CONFIG.game;

  updateState({ job, truck, game });
}

/**
 * Disconnect from local telemetry
 */
export function disconnectLocalTelemetry() {
  if (localSocket) {
    localSocket.close();
    localSocket = null;
  }
}

// =============================================================================
// Trucky API Integration
// =============================================================================

/**
 * Start polling Trucky API for TruckersMP data
 * @param {number[]} playerIds - TruckersMP player IDs for convoy members
 */
export function startTruckyPolling(playerIds = []) {
  stopTruckyPolling();

  const poll = async () => {
    try {
      await fetchTruckyData(playerIds);
    } catch (err) {
      console.error('[Telemetry] Trucky API poll failed:', err);
    }
  };

  // Initial fetch
  poll();

  // Set up interval
  truckyPollTimer = setInterval(poll, CONFIG.truckyPollInterval);
}

/**
 * Stop polling Trucky API
 */
export function stopTruckyPolling() {
  if (truckyPollTimer) {
    clearInterval(truckyPollTimer);
    truckyPollTimer = null;
  }
}

/**
 * Fetch data from Trucky API
 */
async function fetchTruckyData(playerIds) {
  // Fetch game time
  try {
    const timeData = await truckyApi.getGameTime();
    updateState({ gameTime: timeData });
  } catch (err) {
    console.warn('[Telemetry] Failed to fetch game time:', err);
  }

  // Fetch convoy member status if player IDs provided
  if (playerIds.length > 0) {
    const members = await Promise.all(
      playerIds.map(async (id) => {
        try {
          const status = await truckyApi.getPlayerOnlineStatus(id);
          return {
            id,
            name: status.name || `Player ${id}`,
            isOnline: status.online || false,
            isLeader: id === playerIds[0], // First player is leader
          };
        } catch (err) {
          return {
            id,
            name: `Player ${id}`,
            isOnline: false,
            isLeader: id === playerIds[0],
          };
        }
      })
    );

    const onlineMembers = members.filter(m => m.isOnline);

    updateState({
      convoy: {
        active: onlineMembers.length > 0,
        members: onlineMembers,
      },
    });
  }
}

// =============================================================================
// Utility Functions
// =============================================================================

/**
 * Get current state snapshot
 */
export function getState() {
  return { ...state };
}

/**
 * Set game type
 * @param {string} game - 'ats' or 'ets2'
 */
export function setGame(game) {
  CONFIG.game = game;
  updateState({ game });
}

/**
 * Set local telemetry URL
 * @param {string} url - WebSocket URL
 */
export function setLocalTelemetryUrl(url) {
  CONFIG.localTelemetryUrl = url;
}

/**
 * Initialize the telemetry service
 * @param {object} options - Configuration options
 */
export function initialize(options = {}) {
  if (options.game) {
    setGame(options.game);
  }

  if (options.localTelemetryUrl) {
    setLocalTelemetryUrl(options.localTelemetryUrl);
  }

  if (options.connectLocal !== false) {
    connectLocalTelemetry();
  }

  if (options.playerIds) {
    startTruckyPolling(options.playerIds);
  }
}

/**
 * Cleanup and disconnect all services
 */
export function cleanup() {
  disconnectLocalTelemetry();
  stopTruckyPolling();
  listeners = [];
}

export default {
  subscribe,
  initialize,
  cleanup,
  getState,
  setGame,
  connectLocalTelemetry,
  disconnectLocalTelemetry,
  startTruckyPolling,
  stopTruckyPolling,
};
