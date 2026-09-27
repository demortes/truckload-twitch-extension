import { useEffect, useState } from 'react';
import { telemetryService } from '../services';
import mockTelemetry from '../dev/mockTelemetry';

const BACKEND_URL = import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080';

/**
 * Shared telemetry wiring for every view (panel/overlay/mobile/component).
 * Live data by default; falls back to shared mock data only in local dev
 * when there's no Twitch extension context to authorize against.
 */
export function useTelemetry() {
  const [telemetry, setTelemetry] = useState(() =>
    window.Twitch?.ext ? telemetryService.getState() : mockTelemetry
  );

  useEffect(() => {
    if (!window.Twitch?.ext) {
      // Local dev without the Twitch helper (e.g. plain `npm run dev`): nothing to
      // subscribe to, keep showing mock data.
      return undefined;
    }

    const unsubscribe = telemetryService.subscribe(setTelemetry);

    window.Twitch.ext.onAuthorized((auth) => {
      telemetryService.initialize({
        backendUrl: BACKEND_URL,
        channelId: auth.channelId,
      });
    });

    return () => {
      unsubscribe();
      // Note: cleanup() is intentionally not called here. React (StrictMode, and
      // Twitch's own onAuthorized token-refresh calls) can re-run this effect;
      // the service's initialize()/listen() are idempotent, so leaving the
      // PubSub listener attached across a remount is harmless and avoids a
      // flicker of stale/disconnected state.
    };
  }, []);

  return telemetry;
}

export default useTelemetry;
