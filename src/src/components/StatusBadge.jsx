import './StatusBadge.css';

function StatusBadge({ connected, stale }) {
  const offline = !connected || stale;

  return (
    <span className={`status-badge ${offline ? 'offline' : 'online'}`}>
      <span className="status-dot" />
      {offline ? 'OFFLINE' : 'LIVE'}
    </span>
  );
}

export default StatusBadge;
