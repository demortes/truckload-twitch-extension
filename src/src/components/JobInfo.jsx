import '../styles/Panel.css';

function JobInfo({ job }) {
  if (!job || !job.active) {
    return (
      <div className="panel">
        <div className="panel-header">
          <svg className="panel-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2" />
            <rect x="9" y="3" width="6" height="4" rx="1" />
          </svg>
          <span className="panel-title">Current Job</span>
        </div>
        <div className="panel-content">
          <div className="info-block">
            <div className="info-value" style={{ color: 'var(--text-muted)' }}>No Active Job</div>
          </div>
        </div>
      </div>
    );
  }

  const formatTime = (minutes) => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
  };

  return (
    <div className="panel">
      <div className="panel-header">
        <svg className="panel-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2" />
          <rect x="9" y="3" width="6" height="4" rx="1" />
        </svg>
        <span className="panel-title">Current Job</span>
      </div>
      <div className="panel-content">
        <div className="info-row">
          <span className="info-label">Cargo</span>
          <span className="info-value">{job.cargo}</span>
        </div>
        <div className="info-row">
          <span className="info-label">From</span>
          <span className="info-value">{job.source}</span>
        </div>
        <div className="info-row">
          <span className="info-label">To</span>
          <span className="info-value highlight">{job.destination}</span>
        </div>
        <div className="info-row">
          <span className="info-label">Distance</span>
          <span className="info-value">{job.distance} mi</span>
        </div>
        <div className="info-row">
          <span className="info-label">ETA</span>
          <span className={`info-value ${job.etaMinutes <= 30 ? 'success' : ''}`}>
            {formatTime(job.etaMinutes)}
          </span>
        </div>
      </div>
    </div>
  );
}

export default JobInfo;
