import { useState } from 'react';
import { useTelemetry } from '../../hooks/useTelemetry';
import Dashboard from '../../components/Dashboard';
import './component.css';

function formatTime(minutes) {
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return `${hours}h ${mins}m`;
}

function ComponentApp() {
  const { job, truck, dashboard, units, connected, stale } = useTelemetry();
  const [expanded, setExpanded] = useState(false);
  const offline = !connected || stale;

  return (
    <div className={`component-view ${expanded ? 'expanded' : ''}`}>
      <button
        className="component-toggle"
        onClick={() => setExpanded(!expanded)}
        aria-expanded={expanded}
        aria-label={`${expanded ? 'Collapse' : 'Expand'} Truckload panel (${offline ? 'offline' : 'live'})`}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="1" y="3" width="15" height="13" rx="2" />
          <path d="M16 8h4l3 3v5h-7V8z" />
          <circle cx="5.5" cy="18.5" r="2.5" />
          <circle cx="18.5" cy="18.5" r="2.5" />
        </svg>
        <span className={`component-status-dot ${offline ? 'offline' : ''}`} aria-hidden="true" />
      </button>

      {expanded && (
        <div className="component-content">
          {/* Outside the live region: the speed changes every second and must not be announced. */}
          <Dashboard dashboard={dashboard} truck={truck} units={units} live={!offline} variant="compact" />
          <div aria-live="polite">
          {job?.active && (
            <div className="component-section">
              <div className="component-row">
                <span className="component-label">To</span>
                <span className="component-value highlight">{job.destination}</span>
              </div>
              <div className="component-row">
                <span className="component-label">ETA</span>
                <span className="component-value">{formatTime(job.etaMinutes)}</span>
              </div>
            </div>
          )}
          {truck && (
            <div className="component-section">
              <div className="component-row">
                <span className="component-label">Truck</span>
                <span className="component-value">{truck.make}</span>
              </div>
              <div className="component-row">
                <span className="component-label">Fuel</span>
                <span className={`component-value ${truck.fuelPercent < 25 ? 'warning' : ''}`}>
                  {truck.fuelPercent}%
                </span>
              </div>
            </div>
          )}
          {!job?.active && !truck && (
            <div className="component-section">
              <div className="component-row">
                <span className="component-value">{offline ? 'Offline' : 'No active job'}</span>
              </div>
            </div>
          )}
          </div>
        </div>
      )}
    </div>
  );
}

export default ComponentApp;
