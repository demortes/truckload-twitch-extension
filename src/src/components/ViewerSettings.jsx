import { useEffect, useId, useRef, useState } from 'react';
import { FEATURES } from '../config/features';
import { DEFAULT_SETTINGS } from '../services/viewerSettings';
import { useViewerSettings } from '../hooks/useViewerSettings';
import '../styles/ViewerSettings.css';

const UNIT_OPTIONS = [
  ['auto', 'Match the stream'],
  ['imperial', 'Miles and mph'],
  ['metric', 'Kilometers and km/h'],
];
const LAYOUT_OPTIONS = [
  ['standard', 'Standard'],
  ['compact', 'Compact'],
];
const TIME_OPTIONS = [
  ['ago', 'Time ago (5m ago)'],
  ['12h', '12-hour clock'],
  ['24h', '24-hour clock'],
];
const SECTION_OPTIONS = [
  ['job', 'Current job'],
  ['truck', 'Truck'],
  ['history', 'Recent deliveries'],
  ...(FEATURES.convoy ? [['convoy', 'Convoy']] : []),
];

function Radios({ legend, name, options, value, onChange }) {
  return (
    <fieldset className="vs-group">
      <legend>{legend}</legend>
      {options.map(([optionValue, label]) => (
        <label key={optionValue} className="vs-choice">
          <input type="radio" name={name} value={optionValue} checked={value === optionValue} onChange={() => onChange(optionValue)} />
          {label}
        </label>
      ))}
    </fieldset>
  );
}

/**
 * A gear button that opens the viewer's display preferences: units, layout, which sections to show and how
 * delivery times read. Preferences stay in the viewer's browser (see services/viewerSettings.js). Keyboard
 * operable: Escape closes it and returns focus to the button. There is deliberately no aria-live region.
 */
export default function ViewerSettings() {
  const [settings, update] = useViewerSettings();
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const buttonRef = useRef(null);
  const panelId = useId();
  const name = useId();

  useEffect(() => {
    if (!open) return undefined;
    const onPointerDown = (event) => {
      if (rootRef.current && !rootRef.current.contains(event.target)) setOpen(false);
    };
    const onKeyDown = (event) => {
      if (event.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  return (
    <div className="vs-root" ref={rootRef}>
      <button
        ref={buttonRef}
        type="button"
        className="vs-button"
        aria-label="Display settings"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => setOpen(!open)}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="3" />
          <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 11-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 11-2.83-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 110-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 112.83-2.83l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 114 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 112.83 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 110 4h-.09a1.65 1.65 0 00-1.51 1z" />
        </svg>
      </button>

      {open && (
        <div className="vs-panel" id={panelId} role="group" aria-label="Display settings">
          <Radios legend="Units" name={`${name}-units`} options={UNIT_OPTIONS} value={settings.units} onChange={(units) => update({ units })} />
          <Radios legend="Layout" name={`${name}-layout`} options={LAYOUT_OPTIONS} value={settings.layout} onChange={(layout) => update({ layout })} />
          <fieldset className="vs-group">
            <legend>Show</legend>
            {SECTION_OPTIONS.map(([key, label]) => (
              <label key={key} className="vs-choice">
                <input type="checkbox" checked={settings.sections[key]} onChange={(event) => update({ sections: { [key]: event.target.checked } })} />
                {label}
              </label>
            ))}
          </fieldset>
          <Radios legend="Delivery times" name={`${name}-times`} options={TIME_OPTIONS} value={settings.deliveryTimes} onChange={(deliveryTimes) => update({ deliveryTimes })} />
          <button type="button" className="vs-reset" onClick={() => update(DEFAULT_SETTINGS)}>
            Reset to defaults
          </button>
          <p className="vs-note">Saved in this browser only.</p>
        </div>
      )}
    </div>
  );
}