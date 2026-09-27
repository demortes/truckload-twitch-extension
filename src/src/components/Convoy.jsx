import '../styles/Panel.css';

function Convoy({ convoy }) {
  if (!convoy || !convoy.active || !convoy.members || convoy.members.length === 0) {
    return null;
  }

  return (
    <div className="panel">
      <div className="panel-header">
        <svg className="panel-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M23 21v-2a4 4 0 00-3-3.87" />
          <path d="M16 3.13a4 4 0 010 7.75" />
        </svg>
        <span className="panel-title">Convoy</span>
      </div>
      <div className="panel-content">
        <div className="info-row" style={{ marginBottom: 'var(--spacing-sm)' }}>
          <span className="info-label">Drivers</span>
          <span className="info-value highlight">{convoy.members.length}</span>
        </div>
        <div className="convoy-members">
          {convoy.members.map((member, index) => (
            <div key={index} className="info-row">
              <span className="info-value" style={{
                fontSize: '12px',
                textAlign: 'left',
                flex: 1,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap'
              }}>
                {member.name}
              </span>
              {member.isLeader && (
                <span style={{
                  fontSize: '10px',
                  color: 'var(--accent-orange)',
                  marginLeft: 'var(--spacing-xs)'
                }}>
                  LEAD
                </span>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default Convoy;
