// Shared mock telemetry, in the canonical v1 payload shape, used only in local dev
// when no Twitch extension context is present (i.e. no window.Twitch.ext).
const mockTelemetry = {
  connected: true,
  stale: false,
  game: 'ats',
  units: 'imperial',
  job: {
    active: true,
    cargo: 'Electronics',
    source: 'Los Angeles',
    destination: 'Phoenix',
    distance: 372,
    etaMinutes: 245,
  },
  truck: {
    make: 'Peterbilt',
    model: '579',
    licensePlate: 'TRK-4521',
    fuelPercent: 67,
    damagePercent: 3,
    odometer: 124532,
  },
  convoy: {
    active: true,
    members: [
      { name: 'TruckingPro', isLeader: true },
      { name: 'RoadRunner_88', isLeader: false },
      { name: 'HighwayKing', isLeader: false },
    ],
  },
  events: [],
};

export default mockTelemetry;
