// Feature flags for functionality that exists in the codebase but isn't part of
// the v1 release. Toggle here rather than deleting the code.
export const FEATURES = {
  // Convoy (TruckersMP online status via the Trucky API) is deferred to a later
  // release: it needs a way for the broadcaster to configure player IDs first.
  convoy: false,
};
