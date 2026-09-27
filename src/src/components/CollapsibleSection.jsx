import { useState } from 'react';
import '../styles/CollapsibleSection.css';

function CollapsibleSection({ title, icon, defaultExpanded = false, children }) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className={`collapsible-section ${expanded ? 'expanded' : 'collapsed'}`}>
      <button
        className="collapsible-header"
        onClick={() => setExpanded(!expanded)}
        aria-expanded={expanded}
      >
        <div className="collapsible-title-group">
          {icon && <span className="collapsible-icon" aria-hidden="true">{icon}</span>}
          <span className="collapsible-title">{title}</span>
        </div>
        <svg
          className="collapsible-chevron"
          aria-hidden="true"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </button>
      {expanded && (
        <div className="collapsible-content">
          {children}
        </div>
      )}
    </div>
  );
}

export default CollapsibleSection;
