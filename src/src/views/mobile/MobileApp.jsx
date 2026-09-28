import { useState } from 'react';
import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import RecentDeliveries from '../../components/RecentDeliveries';
import StatusBadge from '../../components/StatusBadge';
import { useTelemetry } from '../../hooks/useTelemetry';
import { useJobHistory } from '../../hooks/useJobHistory';
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
  const { job, truck, convoy, units, connected, stale } = useTelemetry();
  const history = useJobHistory();
  const [activeTab, setActiveTab] = useState('job');

  return (
    <div className="mobile-view">
      <nav className="mobile-tabs">
        {TABS.map((tab) => (
          <button
            key={tab}
            className={`mobile-tab ${activeTab === tab ? 'active' : ''}`}
            onClick={() => setActiveTab(tab)}
          >
            {TAB_LABELS[tab]}
          </button>
        ))}
        <StatusBadge connected={connected} stale={stale} />
      </nav>
      <div className="mobile-content">
        {activeTab === 'job' && <JobInfo job={job} units={units} />}
        {activeTab === 'truck' && <TruckStats truck={truck} units={units} />}
        {activeTab === 'history' && <RecentDeliveries history={history} units={units} />}
        {activeTab === 'convoy' && FEATURES.convoy && <Convoy convoy={convoy} />}
      </div>
    </div>
  );
}

export default MobileApp;
