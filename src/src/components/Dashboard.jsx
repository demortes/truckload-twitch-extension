import './Dashboard.css';

/**
 * Live instrument cluster: speed, turn signals, fuel level, headlights, wipers and warning lamps.
 * Fed by the telemetry payload's `dashboard` object (docs/telemetry-contract.md#dashboard) plus
 * `truck.fuelPercent`. Renders nothing when the data isn't live or the bridge is too old to send
 * a dashboard, so every view can include it unconditionally.
 *
 * variant: 'full' (panels / mobile card), 'hud' (video overlay, transparent), 'compact' (video component).
 */

const WARNINGS = {
  fuel: { label: 'Low fuel', level: 'warning' },
  oil: { label: 'Oil pressure', level: 'danger' },
  coolant: { label: 'Coolant temp', level: 'danger' },
  battery: { label: 'Battery', level: 'warning' },
  adblue: { label: 'AdBlue', level: 'warning' },
  air: { label: 'Air pressure', level: 'danger' },
  parkingBrake: { label: 'Parking brake', level: 'warning' },
};

function Arrow({ side, active }) {
  const label = `${side === 'left' ? 'Left' : 'Right'} turn signal ${active ? 'on' : 'off'}`;
  return (
    <svg
      className={`dash-signal dash-signal--${side} ${active ? 'is-on' : ''}`}
      viewBox="0 0 24 24"
      role="img"
      aria-label={label}
    >
      <path d={side === 'left' ? 'M3 12l8-8v5h10v6H11v5z' : 'M21 12l-8-8v5H3v6h10v5z'} />
    </svg>
  );
}

function Chip({ on, label, children }) {
  return (
    <span className={`dash-chip ${on ? 'is-on' : ''}`}>
      <svg className="dash-chip-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
        {children}
      </svg>
      <span className="dash-chip-label">{label}</span>
      <span className="dash-sr">{on ? ' on' : ' off'}</span>
    </span>
  );
}

function fuelClass(percent) {
  if (percent > 50) return '';
  if (percent > 25) return 'warning';
  return 'danger';
}

export default function Dashboard({ dashboard, truck, units = 'imperial', live = true, variant = 'full' }) {
  if (!live || !dashboard) return null;

  const { speed, signal, lights, wipers, warnings = [] } = dashboard;
  const speedUnit = units === 'metric' ? 'km/h' : 'mph';
  const leftOn = signal === 'left' || signal === 'hazard';
  const rightOn = signal === 'right' || signal === 'hazard';
  const fuel = truck?.fuelPercent;
  const activeWarnings = warnings.filter((w) => WARNINGS[w]);

  return (
    <div className={`dashboard dashboard--${variant}`} role="group" aria-label="Truck dashboard">
      <div className="dash-main">
        <Arrow side="left" active={leftOn} />
        <div className="dash-speed">
          <span className="dash-speed-value">{speed}</span>
          <span className="dash-speed-unit">{speedUnit}</span>
        </div>
        <Arrow side="right" active={rightOn} />
      </div>

      {fuel != null && (
        <div className={`dash-fuel ${fuelClass(fuel)}`}>
          <span className="dash-fuel-label">Fuel</span>
          <div
            className="dash-fuel-bar"
            role="meter"
            aria-label="Fuel level"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={fuel}
            aria-valuetext={`${fuel} percent`}
          >
            <div className="dash-fuel-fill" style={{ width: `${Math.max(0, Math.min(100, fuel))}%` }} />
          </div>
          <span className="dash-fuel-value">{fuel}%</span>
        </div>
      )}

      {variant !== 'compact' && (
        <div className="dash-chips">
          <Chip on={lights === 'parking'} label="Park">
            <circle cx="12" cy="12" r="9" />
            <path d="M10 16V8h3a2.5 2.5 0 010 5h-3" />
          </Chip>
          <Chip on={lights === 'low'} label="Low beam">
            <path d="M4 12a6 6 0 016-6h2v12h-2a6 6 0 01-6-6z" />
            <path d="M16 8l5 2M16 12l5 2M16 16l5 2" />
          </Chip>
          <Chip on={lights === 'high'} label="High beam">
            <path d="M4 12a6 6 0 016-6h2v12h-2a6 6 0 01-6-6z" />
            <path d="M16 7h5M16 12h5M16 17h5" />
          </Chip>
          <Chip on={!!wipers} label="Wipers">
            <path d="M3 18a9 9 0 0118 0" />
            <path d="M12 18L8 8" />
          </Chip>
        </div>
      )}

      {activeWarnings.length > 0 && (
        <ul className="dash-warnings" aria-label="Warning lights">
          {activeWarnings.map((w) => (
            <li key={w} className={`dash-warning dash-warning--${WARNINGS[w].level}`}>
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                <path d="M12 9v4M12 17h.01" />
              </svg>
              {WARNINGS[w].label}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
