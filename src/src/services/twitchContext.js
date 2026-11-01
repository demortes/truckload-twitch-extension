/**
 * Shared Twitch context listener (the stream's current game, theme, and so on).
 *
 * Like onAuthorized, the helper keeps one onContext callback, so everything that needs the context
 * subscribes here and a single registration fans it out. See also twitchAuth.js.
 */

const subscribers = new Set();
let latestContext = null;
let registered = false;

function dispatch(context) {
  latestContext = context;
  [...subscribers].forEach((callback) => {
    try {
      callback(context);
    } catch (err) {
      console.error('[TwitchContext] Subscriber failed:', err);
    }
  });
}

/**
 * Calls `callback(context)` with the latest context, now and on every change. Returns an
 * unsubscribe function. A no-op outside a Twitch extension context.
 */
export function onContext(callback) {
  if (!window.Twitch?.ext) return () => {};

  subscribers.add(callback);

  if (!registered) {
    registered = true;
    window.Twitch.ext.onContext(dispatch);
  } else if (latestContext) {
    callback(latestContext);
  }

  return () => {
    subscribers.delete(callback);
  };
}

export default { onContext };
