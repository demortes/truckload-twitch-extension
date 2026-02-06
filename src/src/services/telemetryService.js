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

  // Twitch PubSub
  pubsubTopic: 'broadcast', // Listen to broadcast messages
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
 * Fetch initial telemetry state from the backend API
 * @param {string} backendUrl - Backend base URL
 * @param {string} channelId - Twitch channel ID
 */
async function fetchInitialState(backendUrl, channelId) {
  try {
    const response = await fetch(`${backendUrl}/api/telemetry/${channelId}`);
    if (response.ok) {
      const data = await response.json();
      // If the data is already in our normalized shape, apply directly
      if (data.job || data.truck || data.convoy) {
        updateState(data);
      } else {
        handleLocalTelemetry(data);
      }
      updateState({ connected: true });
    }
  } catch (err) {
    console.warn('[Telemetry] Failed to fetch initial state:', err);
  }
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

  // Fetch initial state from backend if backendUrl and channelId provided
  if (options.backendUrl && options.channelId) {
    fetchInitialState(options.backendUrl, options.channelId);
  }

  if (options.connectLocal !== false) {
    connectLocalTelemetry();
  }

  if (options.playerIds) {
    startTruckyPolling(options.playerIds);
  }

  // Auto-connect to PubSub if Twitch Ext object is present
  if (window.Twitch && window.Twitch.ext) {
    connectTwitchPubSub();
  }
}

/**
 * Cleanup and disconnect all services
 */
export function cleanup() {
  disconnectLocalTelemetry();
  stopTruckyPolling();
  // Twitch PubSub listener cleanup is handled by Twitch Ext lib primarily, 
  // but we could unlisten if needed. 
  if (window.Twitch && window.Twitch.ext) {
    window.Twitch.ext.unlisten('broadcast', handlePubSubMessage);
  }
  listeners = [];
}

// =============================================================================
// Twitch PubSub
// =============================================================================

/**
 * Connect to Twitch PubSub to receive remote telemetry
 */
export function connectTwitchPubSub() {
  if (!window.Twitch || !window.Twitch.ext) {
    console.warn('[Telemetry] Twitch Extension Helper not found. Skipping PubSub.');
    return;
  }

  console.log('[Telemetry] Listening for Twitch PubSub messages...');
  window.Twitch.ext.listen('broadcast', handlePubSubMessage);
}

/**
 * Handle incoming PubSub Message
 */
function handlePubSubMessage(target, contentType, message) {
  // message is a JSON string
  try {
    const data = JSON.parse(message);
    // console.log('[Telemetry] Received PubSub update:', data);
    
    // Determine if this is a 'local' format directly forwarded or needs mapping?
    // The server just stringifies the body from Trucky.
    // If Trucky sends the same format as the local telemetry WebSocket, we can reuse handleLocalTelemetry.
    // However, we might want to flag the source.
    
    // Trucky's formatting might depend on what the "Custom Telemetry" output is.
    // Assuming the user configures Trucky to send the standard JSON output.
    
    handleLocalTelemetry(data);
    updateState({ connected: true }); // Mark as connected since we are receiving data
    
  } catch (err) {
    console.error('[Telemetry] Failed to parse PubSub message:', err);
  }
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
