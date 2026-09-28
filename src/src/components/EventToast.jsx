import { useEffect, useRef, useState } from 'react';
import './EventToast.css';

const TOAST_DURATION_MS = 6000;

const SEVERITY_ICON = {
  critical: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <circle cx="12" cy="12" r="10" />
      <path d="M12 8v4" />
      <path d="M12 16h.01" />
    </svg>
  ),
  warning: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
      <path d="M12 9v4" />
      <path d="M12 17h.01" />
    </svg>
  ),
  info: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <circle cx="12" cy="12" r="10" />
      <path d="M12 16v-4" />
      <path d="M12 8h.01" />
    </svg>
  ),
};

/**
 * A toast-style alert for transient, one-off telemetry events (see
 * docs/telemetry-contract.md#events), e.g. a crash. Renders on top of the existing
 * steady-state panels/overlay without replacing them.
 *
 * `events` is expected to be the current telemetry snapshot's `events` array: non-empty
 * only on the tick something happened, and reset to `[]` on every other tick. Each time a
 * new non-empty batch arrives, its entries are queued as toasts and auto-dismissed after
 * a few seconds, independent of how quickly the underlying `events` array itself reverts
 * to empty.
 */
function EventToast({ events }) {
  const [toasts, setToasts] = useState([]);
  const nextId = useRef(0);

  useEffect(() => {
    if (!Array.isArray(events) || events.length === 0) return undefined;

    const newToasts = events.map((event) => ({ id: nextId.current++, ...event }));
    setToasts((prev) => [...prev, ...newToasts]);

    const timers = newToasts.map((toast) =>
      setTimeout(() => {
        setToasts((prev) => prev.filter((t) => t.id !== toast.id));
      }, TOAST_DURATION_MS)
    );

    return () => timers.forEach(clearTimeout);
  }, [events]);

  if (toasts.length === 0) return null;

  return (
    <div className="event-toast-stack" role="status" aria-live="polite">
      {toasts.map((toast) => (
        <div key={toast.id} className={`event-toast event-toast--${toast.severity || 'info'}`}>
          <span className="event-toast-icon">
            {SEVERITY_ICON[toast.severity] || SEVERITY_ICON.info}
          </span>
          <span className="event-toast-message">{toast.message}</span>
        </div>
      ))}
    </div>
  );
}

export default EventToast;
