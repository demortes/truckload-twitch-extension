import { useState, useEffect } from 'react';
import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import CollapsibleSection from '../../components/CollapsibleSection';
import { telemetryService } from '../../services';
import './panel.css';

// Mock data for development
const mockData = {
  job: {
    active: true,
    cargo: 'Electronics',
    source: 'Los Angeles',
    destination: 'Phoenix',
    distance: 372,
    etaMinutes: 245
  },
  truck: {
    make: 'Peterbilt',
    model: '579',
    licensePlate: 'TRK-4521',
    fuelPercent: 67,
    damagePercent: 3,
    odometer: 124532
  },
  convoy: {
    active: true,
    members: [
      { name: 'TruckingPro', isLeader: true },
      { name: 'RoadRunner_88', isLeader: false },
      { name: 'HighwayKing', isLeader: false }
    ]
  }
};

const USE_LIVE_TELEMETRY = false;
const BACKEND_URL = import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080';

const JobIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2" />
    <rect x="9" y="3" width="6" height="4" rx="1" />
  </svg>
);

const TruckIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <rect x="1" y="3" width="15" height="13" rx="2" />
    <path d="M16 8h4l3 3v5h-7V8z" />
    <circle cx="5.5" cy="18.5" r="2.5" />
    <circle cx="18.5" cy="18.5" r="2.5" />
  </svg>
);

const ConvoyIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" />
    <circle cx="9" cy="7" r="4" />
    <path d="M23 21v-2a4 4 0 00-3-3.87" />
    <path d="M16 3.13a4 4 0 010 7.75" />
  </svg>
);

function PanelApp() {
  const [telemetry, setTelemetry] = useState(mockData);

  useEffect(() => {
    if (!USE_LIVE_TELEMETRY) return;

    const unsubscribe = telemetryService.subscribe((state) => {
      if (state.job || state.truck || state.convoy) {
        setTelemetry({
          job: state.job || mockData.job,
          truck: state.truck || mockData.truck,
          convoy: state.convoy || mockData.convoy,
        });
      }
    });

    // Initialize with Twitch auth for channel-aware backend fetching
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
      // Fallback for local dev without Twitch
      telemetryService.initialize({ game: 'ats', connectLocal: true });
    }

    return () => {
      unsubscribe();
      telemetryService.cleanup();
    };
  }, []);

  return (
    <div className="panel-view">
      <div className="panel-header-bar">
        <span className="panel-logo">TRUCKLOAD</span>
      </div>
      <div className="panel-content-area">
        <CollapsibleSection title="Current Job" icon={JobIcon} defaultExpanded={true}>
          <JobInfo job={telemetry.job} />
        </CollapsibleSection>
        <CollapsibleSection title="Truck" icon={TruckIcon} defaultExpanded={false}>
          <TruckStats truck={telemetry.truck} />
        </CollapsibleSection>
        <CollapsibleSection title="Convoy" icon={ConvoyIcon} defaultExpanded={false}>
          <Convoy convoy={telemetry.convoy} />
        </CollapsibleSection>
      </div>
    </div>
  );
}

export default PanelApp;
