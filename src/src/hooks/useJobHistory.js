import { useEffect, useState } from 'react';
import mockJobHistory from '../dev/mockJobHistory';

const BACKEND_URL = import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080';

// There's no live push channel for history the way there is for the main telemetry
// snapshot (see useTelemetry/telemetryService) — a completed job doesn't broadcast
// anything of its own — so this re-fetches on a slow interval instead.
const POLL_INTERVAL_MS = 60_000;

/**
 * Fetches a channel's recent completed-job history from the backend
 * (GET /api/telemetry/{channelId}/history). Live data by default; falls back to
 * shared mock data only in local dev when there's no Twitch extension context
 * to authorize against.
 */
export function useJobHistory() {
  const [history, setHistory] = useState(() => (window.Twitch?.ext ? [] : mockJobHistory));

  useEffect(() => {
    if (!window.Twitch?.ext) {
      // Local dev without the Twitch helper (e.g. plain `npm run dev`): nothing to
      // authorize against, keep showing mock data.
      return undefined;
    }

    let cancelled = false;
    let pollTimer = null;
    let channelId = null;

    const fetchHistory = async () => {
      if (!channelId) return;
      try {
        const response = await fetch(`${BACKEND_URL}/api/telemetry/${channelId}/history`);
        if (response.ok && !cancelled) {
          setHistory(await response.json());
        }
      } catch (err) {
        console.warn('[JobHistory] Failed to fetch history:', err);
      }
    };

    window.Twitch.ext.onAuthorized((auth) => {
      channelId = auth.channelId;
      fetchHistory();
      if (!pollTimer) {
        pollTimer = setInterval(fetchHistory, POLL_INTERVAL_MS);
      }
    });

    return () => {
      cancelled = true;
      if (pollTimer) clearInterval(pollTimer);
    };
  }, []);

  return history;
}

export default useJobHistory;
