import { useState } from 'react';
import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import RecentDeliveries from '../../components/RecentDeliveries';
import StatusBadge from '../../components/StatusBadge';
import Dashboard from '../../components/Dashboard';
import ViewerSettings from '../../components/ViewerSettings';
import { useTelemetry } from '../../hooks/useTelemetry';
import { useJobHistory } from '../../hooks/useJobHistory';
import { useViewerSettings } from '../../hooks/useViewerSettings';
import { useViewerUnits } from '../../hooks/useViewerUnits';
import { FEATURES } from '../../config/features';
import './mobile.css';

const TABS = FEATURES.convoy
  ? ['job', 'truck', 'history', 'convoy']
  : ['job', 'truck', 'history'];

const TAB_LABELS = {
  job: 'Job',
  truck: 'Truck',
  history: 'History',
  convoy: 'Convoy',
};

function MobileApp() {
  const telemetry = useTelemetry();
  const { convoy, connected, stale } = telemetry;
  const history = useJobHistory();
  const { job, truck, dashboard, units, history: shownHistory } = useViewerUnits({ ...telemetry, history });
  const [settings] = useViewerSettings();
  const [requestedTab, setActiveTab] = useState('job');

  // Tabs follow the sections this viewer chose to show; if the open one was hidden, fall back to the first left.
  const visibleTabs = TABS.filter((tab) => settings.sections[tab]);
  const activeTab = visibleTabs.includes(requestedTab) ? requestedTab : visibleTabs[0];

  return (
    <div className={`mobile-view ${settings.layout === 'compact' ? 'layout-compact' : ''}`}>
      <nav className="mobile-tabs">
        {visibleTabs.map((tab) => (
          <button
            key={tab}
            className={`mobile-tab ${activeTab === tab ? 'active' : ''}`}
            onClick={() => setActiveTab(tab)}
            aria-current={activeTab === tab ? 'true' : undefined}
          >
            {TAB_LABELS[tab]}
          </button>
        ))}
        <span className="vs-actions mobile-actions">
          <StatusBadge connected={connected} stale={stale} />
          <ViewerSettings />
        </span>
      </nav>
      <div className="mobile-content">
        <Dashboard dashboard={dashboard} truck={truck} units={units} live={connected && !stale} variant="full" />
        {activeTab === 'job' && <JobInfo job={job} units={units} />}
        {activeTab === 'truck' && <TruckStats truck={truck} units={units} />}
        {activeTab === 'history' && <RecentDeliveries history={shownHistory} units={units} deliveryTimes={settings.deliveryTimes} />}
        {activeTab === 'convoy' && FEATURES.convoy && <Convoy convoy={convoy} />}
      </div>
    </div>
  );
}

export default MobileApp;
