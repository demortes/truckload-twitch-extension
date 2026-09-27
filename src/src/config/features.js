// Feature flags for functionality that exists in the codebase but isn't part of
// the v1 release. Toggle here rather than deleting the code.
export const FEATURES = {
  // Convoy (TruckersMP online status via the Trucky API). The broadcaster
  // configures player IDs on the config view (Twitch Configuration Service,
  // broadcaster segment); see src/src/services/convoyConfig.js.
  convoy: true,
};
