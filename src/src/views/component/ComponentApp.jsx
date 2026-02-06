import { useState, useEffect } from 'react';
import { telemetryService } from '../../services';
import './component.css';

// Mock data for development
const mockData = {
  job: {
    active: true,
    cargo: 'Electronics',
    destination: 'Phoenix',
    etaMinutes: 245
  },
  truck: {
    make: 'Peterbilt',
    model: '579',
    fuelPercent: 67
  }
};

const USE_LIVE_TELEMETRY = false;
const BACKEND_URL = import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080';

function ComponentApp() {
  const [telemetry, setTelemetry] = useState(mockData);
  const [expanded, setExpanded] = useState(false);

  useEffect(() => {
    if (!USE_LIVE_TELEMETRY) return;

    const unsubscribe = telemetryService.subscribe((state) => {
      if (state.job || state.truck) {
        setTelemetry({
          job: state.job || mockData.job,
          truck: state.truck || mockData.truck,
        });
      }
    });

    if (window.Twitch?.ext) {
      window.Twitch.ext.onAuthorized((auth) => {
        telemetryService.initialize({
          game: 'ats',
          connectLocal: false,
          backendUrl: BACKEND_URL,
          channelId: auth.channelId,
        });
      });
    } else {
      telemetryService.initialize({ game: 'ats', connectLocal: true });
    }

    return () => {
      unsubscribe();
      telemetryService.cleanup();
    };
  }, []);

  const formatTime = (minutes) => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
  };

  return (
    <div className={`component-view ${expanded ? 'expanded' : ''}`}>
      <button
        className="component-toggle"
        onClick={() => setExpanded(!expanded)}
        aria-label={expanded ? 'Collapse' : 'Expand'}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="1" y="3" width="15" height="13" rx="2" />
          <path d="M16 8h4l3 3v5h-7V8z" />
          <circle cx="5.5" cy="18.5" r="2.5" />
          <circle cx="18.5" cy="18.5" r="2.5" />
        </svg>
      </button>

      {expanded && (
        <div className="component-content">
          {telemetry.job?.active && (
            <div className="component-section">
              <div className="component-row">
                <span className="component-label">To</span>
                <span className="component-value highlight">{telemetry.job.destination}</span>
              </div>
              <div className="component-row">
                <span className="component-label">ETA</span>
                <span className="component-value">{formatTime(telemetry.job.etaMinutes)}</span>
              </div>
            </div>
          )}
          <div className="component-section">
            <div className="component-row">
              <span className="component-label">Truck</span>
              <span className="component-value">{telemetry.truck.make}</span>
            </div>
            <div className="component-row">
              <span className="component-label">Fuel</span>
              <span className={`component-value ${telemetry.truck.fuelPercent < 25 ? 'warning' : ''}`}>
                {telemetry.truck.fuelPercent}%
              </span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ComponentApp;
