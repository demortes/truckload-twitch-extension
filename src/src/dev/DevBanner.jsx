import { useEffect, useState } from 'react';
import { telemetryService } from '../services';
import { getDevBackend } from './devBackend';
import { describeDevStatus } from './devStatusText';

const COLORS = { info: '#3b82f6', ok: '#22c55e', warn: '#f59e0b', error: '#ef4444' };

/**
 * Dev-only strip pinned to a corner of the page: says whether the view is on mock data or live data from
 * which backend and channel, how old the data is, and what went wrong if nothing is arriving. It is
 * mounted on its own root (mountDevBanner.jsx) so it still shows when a view renders nothing at all
 * (the overlay hides itself when the streamer is offline).
 */
export default function DevBanner() {
  const [dev] = useState(() => getDevBackend());
  const [telemetry, setTelemetry] = useState(() => telemetryService.getState());
  const [status, setStatus] = useState(() => telemetryService.getDevStatus());
  const [now, setNow] = useState(Date.now);

  useEffect(() => {
    const unsubscribeTelemetry = telemetryService.subscribe(setTelemetry);
    const unsubscribeStatus = telemetryService.subscribeDevStatus(setStatus);
    const tick = setInterval(() => setNow(Date.now()), 1000);
    return () => {
      unsubscribeTelemetry();
      unsubscribeStatus();
      clearInterval(tick);
    };
  }, []);

  const { level, text } = describeDevStatus({ dev, status, telemetry, now });

  return (
    <div
      role="status"
      style={{
        position: 'fixed',
        left: 0,
        right: 0,
        bottom: 0,
        zIndex: 2147483647,
        pointerEvents: 'none',
        padding: '3px 8px',
        font: '11px/1.4 ui-monospace, Consolas, monospace',
        color: '#fff',
        background: 'rgba(15, 15, 20, 0.88)',
        borderTop: `2px solid ${COLORS[level]}`,
      }}
    >
      {text}
    </div>
  );
}