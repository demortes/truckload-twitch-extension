import '../styles/Panel.css';
import { formatDeliveryTime } from '../services/deliveryTime';


/**
 * Session-scoped "recent deliveries" feed: the last few jobs completed on this
 * channel, newest first, so a viewer who joins mid-stream can see what's
 * already been delivered. Backed by GET /api/telemetry/{channelId}/history
 * (see hooks/useJobHistory).
 */
function RecentDeliveries({ history = [], units = 'imperial', deliveryTimes = 'ago' }) {
  const distanceUnit = units === 'metric' ? 'km' : 'mi';

  return (
    <div className="panel">
      <div className="panel-header">
        <svg className="panel-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M20 6L9 17l-5-5" />
        </svg>
        <span className="panel-title">Recent Deliveries</span>
      </div>
      <div className="panel-content">
        {history.length === 0 ? (
          <div className="info-block">
            <div className="info-value" style={{ color: 'var(--text-muted)' }}>No completed deliveries yet</div>
          </div>
        ) : (
          <div className="job-history-list">
            {history.map((entry) => (
              <div className="job-history-item" key={entry.id}>
                <div className="job-history-cargo">{entry.cargo}</div>
                <div className="job-history-route">{entry.source} → {entry.destination}</div>
                <div className="job-history-meta">
                  {entry.distance} {distanceUnit} · {formatDeliveryTime(entry.completedAt, deliveryTimes)}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default RecentDeliveries;
