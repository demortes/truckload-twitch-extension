// Shared mock job history, in the shape returned by GET /api/telemetry/{channelId}/history,
// used only in local dev when no Twitch extension context is present (i.e. no window.Twitch.ext).
const mockJobHistory = [
  {
    id: 'mock-3',
    cargo: 'Frozen Foods',
    source: 'Denver',
    destination: 'Salt Lake City',
    distance: 508,
    completedAt: new Date(Date.now() - 25 * 60_000).toISOString(),
  },
  {
    id: 'mock-2',
    cargo: 'Machine Parts',
    source: 'Phoenix',
    destination: 'Albuquerque',
    distance: 328,
    completedAt: new Date(Date.now() - 95 * 60_000).toISOString(),
  },
  {
    id: 'mock-1',
    cargo: 'Electronics',
    source: 'Los Angeles',
    destination: 'Phoenix',
    distance: 372,
    completedAt: new Date(Date.now() - 160 * 60_000).toISOString(),
  },
];

export default mockJobHistory;
