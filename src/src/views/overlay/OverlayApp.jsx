import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import StatusBadge from '../../components/StatusBadge';
import Dashboard from '../../components/Dashboard';
import EventToast from '../../components/EventToast';
import { useTelemetry } from '../../hooks/useTelemetry';
import { useOverlayVisibility } from '../../hooks/useOverlayVisibility';
import { useViewerUnits } from '../../hooks/useViewerUnits';
import { FEATURES } from '../../config/features';
import './overlay.css';

function OverlayApp() {
  const telemetry = useTelemetry();
  const { convoy, connected, stale, lastLiveAt, events } = telemetry;
  // The viewer's own units (set in the panel or mobile app) apply here too.
  const { job, truck, dashboard, units } = useViewerUnits(telemetry);
  const visible = useOverlayVisibility({ connected, stale, lastLiveAt });

  // Nothing over the stream while they are not playing.
  if (!visible) return null;

  return (
    <div className="overlay">
      <EventToast events={events} />
      <div className="overlay-center">
        <Dashboard dashboard={dashboard} truck={truck} units={units} live={connected && !stale} variant="hud" />
      </div>
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
