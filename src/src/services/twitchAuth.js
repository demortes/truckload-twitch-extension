/**
 * Shared Twitch authorization listener.
 *
 * Twitch's helper (twitch-ext.min.js) keeps exactly ONE `onAuthorized` callback: every call to
 * `Twitch.ext.onAuthorized(cb)` first removes whatever was registered before. When two hooks on the
 * same view each register their own callback (the panel and mobile views use both useTelemetry and
 * useJobHistory), the later one silently replaces the earlier, which then never runs.
 *
 * This module registers a single callback with the helper and fans the authorization out to any
 * number of subscribers, replaying the latest authorization to late subscribers. Always use this
 * instead of calling `Twitch.ext.onAuthorized` directly.
 */

const subscribers = new Set();
let latestAuth = null;
let registered = false;

function dispatch(auth) {
  latestAuth = auth;
  [...subscribers].forEach((callback) => {
    try {
      callback(auth);
    } catch (err) {
      // One subscriber failing must not stop the others from being authorized.
      console.error('[TwitchAuth] Subscriber failed:', err);
    }
  });
}

/**
 * Calls `callback(auth)` once authorization is available and again whenever Twitch refreshes it.
 * Returns an unsubscribe function. A no-op outside a Twitch extension context.
 */
export function onAuthorized(callback) {
  if (!window.Twitch?.ext) return () => {};

  subscribers.add(callback);

  if (!registered) {
    registered = true;
    // The helper invokes the callback immediately if it already has auth, which reaches
    // this callback through dispatch().
    window.Twitch.ext.onAuthorized(dispatch);
  } else if (latestAuth) {
    callback(latestAuth);
  }

  return () => {
    subscribers.delete(callback);
  };
}

export default { onAuthorized };
