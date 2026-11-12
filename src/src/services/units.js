// Per-viewer unit conversion. The payload's numbers are already in the stream's units (mph and miles, or
// km/h and km, as chosen by the bridge), so showing a viewer the other system means converting the numbers,
// not just swapping the label.

const KM_PER_MILE = 1.609344;
const SYSTEMS = ['imperial', 'metric'];

/** 'auto' follows the stream; an explicit choice wins. Anything unrecognised falls back to the stream. */
export function effectiveUnits(preference, streamUnits) {
  const stream = SYSTEMS.includes(streamUnits) ? streamUnits : 'imperial';
  return SYSTEMS.includes(preference) ? preference : stream;
}

function convert(value, from, to) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return value;
  if (from === to) return value;
  return Math.round(from === 'imperial' ? value * KM_PER_MILE : value / KM_PER_MILE);
}

/** Miles to km or back, rounded to a whole number. Missing or non-numeric values pass through. */
export const convertDistance = convert;

/** mph to km/h or back, rounded to a whole number. */
export const convertSpeed = convert;

/**
 * Returns the telemetry the viewer should see: every distance and speed converted into their preferred
 * system, with `units` set to what is now shown (so the existing components label it correctly). Returns the
 * SAME object when nothing needs converting, so React can skip re-rendering.
 *
 * @param {{units: string, job?: object|null, truck?: object|null, dashboard?: object|null, history?: object[]}} data
 * @param {string} preference 'auto' | 'imperial' | 'metric'
 */
export function applyViewerUnits(data, preference) {
  const from = SYSTEMS.includes(data.units) ? data.units : 'imperial';
  const to = effectiveUnits(preference, from);
  if (to === from) return data;

  return {
    ...data,
    units: to,
    job: data.job ? { ...data.job, distance: convert(data.job.distance, from, to) } : data.job,
    truck: data.truck ? { ...data.truck, odometer: convert(data.truck.odometer, from, to) } : data.truck,
    dashboard: data.dashboard ? { ...data.dashboard, speed: convert(data.dashboard.speed, from, to) } : data.dashboard,
    history: data.history ? data.history.map((entry) => ({ ...entry, distance: convert(entry.distance, from, to) })) : data.history,
  };
}