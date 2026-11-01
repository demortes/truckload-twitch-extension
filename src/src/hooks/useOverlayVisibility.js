import { useEffect, useState } from 'react';
import { onConfigChanged, getShowWhenOffline } from '../services/convoyConfig';
import { onContext } from '../services/twitchContext';
import { shouldShowOverlay } from '../services/visibility';

const TICK_MS = 5_000;

/**
 * Whether a view that sits over the stream video should render at all. It stays up while the
 * streamer is playing (and for a couple of minutes after), and gets out of the way when they are
 * not, or when Twitch says they are in a non-truck category. A broadcaster setting can keep it up.
 * The rules live in services/visibility.js.
 *
 * @param {{connected: boolean, stale: boolean, lastLiveAt: number|null}} telemetry
 */
export function useOverlayVisibility({ connected, stale, lastLiveAt }) {
  const [now, setNow] = useState(Date.now);
  const [game, setGame] = useState('');
  const [showWhenOffline, setShowWhenOffline] = useState(() => getShowWhenOffline());

  // The grace period ends by the clock, not by an incoming message, so tick to re-evaluate.
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), TICK_MS);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => onContext((context) => setGame(context?.game ?? '')), []);

  useEffect(() => onConfigChanged((config) => setShowWhenOffline(config.showWhenOffline)), []);

  return shouldShowOverlay({ connected, stale, lastLiveAt, now, game, showWhenOffline });
}
