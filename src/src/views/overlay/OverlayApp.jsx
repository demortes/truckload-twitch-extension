import { useState, useEffect } from 'react';
import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import { telemetryService } from '../../services';
import './overlay.css';

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

function OverlayApp() {
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

    telemetryService.initialize({ game: 'ats', connectLocal: true });

    return () => {
      unsubscribe();
      telemetryService.cleanup();
    };
  }, []);

  return (
    <div className="overlay">
      <div className="overlay-left">
        <JobInfo job={telemetry.job} />
      </div>
      <div className="overlay-right">
        <TruckStats truck={telemetry.truck} />
        <Convoy convoy={telemetry.convoy} />
      </div>
    </div>
  );
}

export default OverlayApp;
