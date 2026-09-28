import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import RecentDeliveries from '../../components/RecentDeliveries';
import CollapsibleSection from '../../components/CollapsibleSection';
import StatusBadge from '../../components/StatusBadge';
import EventToast from '../../components/EventToast';
import { useTelemetry } from '../../hooks/useTelemetry';
import { useJobHistory } from '../../hooks/useJobHistory';
import { FEATURES } from '../../config/features';
import './panel.css';

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

const HistoryIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M20 6L9 17l-5-5" />
  </svg>
);

function PanelApp() {
  const { job, truck, convoy, units, connected, stale, events } = useTelemetry();
  const history = useJobHistory();

  return (
    <div className="panel-view">
      <EventToast events={events} />
      <div className="panel-header-bar">
        <span className="panel-logo">TRUCKLOAD</span>
        <StatusBadge connected={connected} stale={stale} />
      </div>
      <div className="panel-content-area">
        <CollapsibleSection title="Current Job" icon={JobIcon} defaultExpanded={true}>
          <JobInfo job={job} units={units} />
        </CollapsibleSection>
        <CollapsibleSection title="Truck" icon={TruckIcon} defaultExpanded={false}>
          <TruckStats truck={truck} units={units} />
        </CollapsibleSection>
        <CollapsibleSection title="Recent Deliveries" icon={HistoryIcon} defaultExpanded={false}>
          <RecentDeliveries history={history} units={units} />
        </CollapsibleSection>
        {FEATURES.convoy && (
          <CollapsibleSection title="Convoy" icon={ConvoyIcon} defaultExpanded={false}>
            <Convoy convoy={convoy} />
          </CollapsibleSection>
        )}
      </div>
    </div>
  );
}

export default PanelApp;
