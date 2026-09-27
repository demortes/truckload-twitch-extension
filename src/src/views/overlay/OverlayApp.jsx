import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import StatusBadge from '../../components/StatusBadge';
import { useTelemetry } from '../../hooks/useTelemetry';
import { FEATURES } from '../../config/features';
import './overlay.css';

function OverlayApp() {
  const { job, truck, convoy, units, connected, stale } = useTelemetry();

  return (
    <div className="overlay">
      <div className="overlay-left">
        <JobInfo job={job} units={units} />
      </div>
      <div className="overlay-right">
        <TruckStats truck={truck} units={units} />
        {FEATURES.convoy && <Convoy convoy={convoy} />}
        <StatusBadge connected={connected} stale={stale} />
      </div>
    </div>
  );
}

export default OverlayApp;
