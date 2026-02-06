import { useState, useEffect } from 'react';
import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import { telemetryService } from '../../services';
import './mobile.css';

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

function MobileApp() {
  const [telemetry, setTelemetry] = useState(mockData);
  const [activeTab, setActiveTab] = useState('job');

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

  return (
    <div className="mobile-view">
      <nav className="mobile-tabs">
        <button
          className={`mobile-tab ${activeTab === 'job' ? 'active' : ''}`}
          onClick={() => setActiveTab('job')}
        >
          Job
        </button>
        <button
          className={`mobile-tab ${activeTab === 'truck' ? 'active' : ''}`}
          onClick={() => setActiveTab('truck')}
        >
          Truck
        </button>
        <button
          className={`mobile-tab ${activeTab === 'convoy' ? 'active' : ''}`}
          onClick={() => setActiveTab('convoy')}
        >
          Convoy
        </button>
      </nav>
      <div className="mobile-content">
        {activeTab === 'job' && <JobInfo job={telemetry.job} />}
        {activeTab === 'truck' && <TruckStats truck={telemetry.truck} />}
        {activeTab === 'convoy' && <Convoy convoy={telemetry.convoy} />}
      </div>
    </div>
  );
}

export default MobileApp;
