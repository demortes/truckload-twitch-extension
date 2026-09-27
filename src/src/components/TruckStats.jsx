import '../styles/Panel.css';

function TruckStats({ truck, units = 'imperial' }) {
  if (!truck) {
    return null;
  }

  const distanceUnit = units === 'metric' ? 'km' : 'mi';

  const getFuelClass = (percent) => {
    if (percent > 50) return '';
    if (percent > 25) return 'warning';
    return 'danger';
  };

  const getDamageClass = (percent) => {
    if (percent < 10) return 'success';
    if (percent < 30) return 'warning';
    return 'danger';
  };

  return (
    <div className="panel">
      <div className="panel-header">
        <svg className="panel-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="1" y="3" width="15" height="13" rx="2" />
          <path d="M16 8h4l3 3v5h-7V8z" />
          <circle cx="5.5" cy="18.5" r="2.5" />
          <circle cx="18.5" cy="18.5" r="2.5" />
        </svg>
        <span className="panel-title">Truck</span>
      </div>
      <div className="panel-content">
        <div className="info-row">
          <span className="info-label">Make</span>
          <span className="info-value">{truck.make}</span>
        </div>
        <div className="info-row">
          <span className="info-label">Model</span>
          <span className="info-value">{truck.model}</span>
        </div>
        {truck.licensePlate && (
          <div className="info-row">
            <span className="info-label">Plate</span>
            <span className="info-value" style={{ fontFamily: 'monospace' }}>{truck.licensePlate}</span>
          </div>
        )}
        <div className="info-row">
          <span className="info-label">Fuel</span>
          <span className={`info-value ${getFuelClass(truck.fuelPercent)}`}>
            {truck.fuelPercent}%
          </span>
        </div>
        <div className="info-row">
          <span className="info-label">Damage</span>
          <span className={`info-value ${getDamageClass(truck.damagePercent)}`}>
            {truck.damagePercent}%
          </span>
        </div>
        {truck.odometer !== undefined && (
          <div className="info-row">
            <span className="info-label">Odometer</span>
            <span className="info-value">{truck.odometer.toLocaleString()} {distanceUnit}</span>
          </div>
        )}
      </div>
    </div>
  );
}

export default TruckStats;
