import { useState } from 'react';
import JobInfo from '../../components/JobInfo';
import TruckStats from '../../components/TruckStats';
import Convoy from '../../components/Convoy';
import StatusBadge from '../../components/StatusBadge';
import { useTelemetry } from '../../hooks/useTelemetry';
import { FEATURES } from '../../config/features';
import './mobile.css';

const TABS = FEATURES.convoy ? ['job', 'truck', 'convoy'] : ['job', 'truck'];

function MobileApp() {
  const { job, truck, convoy, units, connected, stale } = useTelemetry();
  const [activeTab, setActiveTab] = useState('job');

  return (
    <div className="mobile-view">
      <nav className="mobile-tabs">
        {TABS.map((tab) => (
          <button
            key={tab}
            className={`mobile-tab ${activeTab === tab ? 'active' : ''}`}
            onClick={() => setActiveTab(tab)}
            aria-current={activeTab === tab ? 'true' : undefined}
          >
            {tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
        <StatusBadge connected={connected} stale={stale} />
      </nav>
      <div className="mobile-content">
        {activeTab === 'job' && <JobInfo job={job} units={units} />}
        {activeTab === 'truck' && <TruckStats truck={truck} units={units} />}
        {activeTab === 'convoy' && FEATURES.convoy && <Convoy convoy={convoy} />}
      </div>
    </div>
  );
}

export default MobileApp;
