import './StatusBadge.css';

function StatusBadge({ connected, stale }) {
  const offline = !connected || stale;

  return (
    <span
      className={`status-badge ${offline ? 'offline' : 'online'}`}
      role="status"
      aria-live="polite"
    >
      <span className="status-dot" aria-hidden="true" />
      {offline ? 'OFFLINE' : 'LIVE'}
    </span>
  );
}

export default StatusBadge;
