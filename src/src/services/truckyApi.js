/**
 * Trucky API Service
 *
 * A client for the Trucky REST API providing access to TruckersMP,
 * ETS2/ATS game data, traffic, and player information.
 *
 * API Documentation: https://api.truckyapp.com/docs/
 * No authentication required for public endpoints.
 */

const BASE_URL = 'https://api.truckyapp.com/v2';
const USER_AGENT = 'Truckload-Twitch-Extension/1.0';

/**
 * Base fetch wrapper with required headers
 */
async function apiRequest(endpoint, options = {}) {
  const url = `${BASE_URL}${endpoint}`;

  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'User-Agent': USER_AGENT,
      ...options.headers,
    },
  });

  if (!response.ok) {
    throw new ApiError(
      `Trucky API error: ${response.status} ${response.statusText}`,
      response.status
    );
  }

  const data = await response.json();
  return data.response || data;
}

/**
 * Custom error class for API errors
 */
class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

// =============================================================================
// Server & Game Data
// =============================================================================

/**
 * Get list of TruckersMP servers with player counts
 */
export async function getServers() {
  return apiRequest('/truckersmp/servers');
}

/**
 * Get current TruckersMP game version info
 */
export async function getGameVersion() {
  return apiRequest('/truckersmp/version');
}

/**
 * Get current in-game time
 */
export async function getGameTime() {
  return apiRequest('/truckersmp/time');
}

/**
 * Get TruckersMP rules
 */
export async function getRules() {
  return apiRequest('/truckersmp/rules');
}

// =============================================================================
// Traffic Data
// =============================================================================

/**
 * Get traffic data for a specific server
 * @param {string} server - Server short name (e.g., 'sim1', 'sim2')
 * @param {string} game - Game identifier ('ets2' or 'ats')
 */
export async function getTraffic(server, game = 'ats') {
  return apiRequest(`/traffic?server=${server}&game=${game.toLowerCase()}`);
}

/**
 * Get list of servers with traffic data available
 */
export async function getTrafficServers() {
  return apiRequest('/traffic/servers');
}

// =============================================================================
// Player Information
// =============================================================================

/**
 * Get TruckersMP player info by ID
 * @param {number} playerId - TruckersMP player ID
 */
export async function getPlayer(playerId) {
  return apiRequest(`/truckersmp/player/${playerId}`);
}

/**
 * Search for a player by various identifiers
 * @param {string} query - Search term
 * @param {string} type - Search type: 'steamid', 'truckersmpid', or 'steamusername'
 */
export async function searchPlayer(query, type = 'steamusername') {
  return apiRequest(`/steam/searchPlayer?query=${encodeURIComponent(query)}&searchType=${type}`);
}

/**
 * Check if a player is online
 * @param {number} playerId - TruckersMP player ID
 */
export async function isPlayerOnline(playerId) {
  return apiRequest(`/truckersmp/isOnline/${playerId}`);
}

/**
 * Check if a player is online with enhanced location data
 * @param {number} playerId - TruckersMP player ID
 */
export async function getPlayerOnlineStatus(playerId) {
  return apiRequest(`/truckersmp/isOnlineImproved/${playerId}`);
}

/**
 * Get player bans history
 * @param {number} playerId - TruckersMP player ID
 */
export async function getPlayerBans(playerId) {
  return apiRequest(`/truckersmp/bans/${playerId}`);
}

/**
 * Get complete player info including Steam data
 * @param {string} query - Player identifier
 */
export async function getPlayerComplete(query) {
  return apiRequest(`/steam/getPlayerInfoComplete?query=${encodeURIComponent(query)}`);
}

// =============================================================================
// Location Data
// =============================================================================

/**
 * Get ETS2 cities with coordinates
 */
export async function getEts2Cities() {
  return apiRequest('/map/ets2/cities');
}

/**
 * Get ATS cities with coordinates
 */
export async function getAtsCities() {
  return apiRequest('/map/ats/cities');
}

/**
 * Get points of interest from ETS2Map
 */
export async function getPois() {
  return apiRequest('/map/pois');
}

/**
 * Get map servers list
 */
export async function getMapServers() {
  return apiRequest('/map/servers');
}

// =============================================================================
// News & Events
// =============================================================================

/**
 * Get TruckersMP news
 */
export async function getNews() {
  return apiRequest('/truckersmp/news');
}

/**
 * Get ETS2 Steam news
 */
export async function getEts2News() {
  return apiRequest('/steam/news/ets2');
}

/**
 * Get ATS Steam news
 */
export async function getAtsNews() {
  return apiRequest('/steam/news/ats');
}

/**
 * Get upcoming events
 */
export async function getEvents() {
  return apiRequest('/events');
}

// =============================================================================
// Streams & Media
// =============================================================================

/**
 * Get ETS2 Twitch streams
 */
export async function getEts2Streams() {
  return apiRequest('/streams/twitch/ets2');
}

/**
 * Get ATS Twitch streams
 */
export async function getAtsStreams() {
  return apiRequest('/streams/twitch/ats');
}

// =============================================================================
// Utility Exports
// =============================================================================

export { ApiError };

export default {
  // Server & Game
  getServers,
  getGameVersion,
  getGameTime,
  getRules,

  // Traffic
  getTraffic,
  getTrafficServers,

  // Player
  getPlayer,
  searchPlayer,
  isPlayerOnline,
  getPlayerOnlineStatus,
  getPlayerBans,
  getPlayerComplete,

  // Location
  getEts2Cities,
  getAtsCities,
  getPois,
  getMapServers,

  // News & Events
  getNews,
  getEts2News,
  getAtsNews,
  getEvents,

  // Streams
  getEts2Streams,
  getAtsStreams,
};
