import { useMemo } from 'react';
import { applyViewerUnits } from '../services/units';
import { useViewerSettings } from './useViewerSettings';

/**
 * Converts the telemetry (and delivery history) into the units this viewer chose. Components keep taking
 * `units` for their labels; they just receive already-converted numbers and the matching `units`.
 *
 * @param {{units: string, job?: object|null, truck?: object|null, dashboard?: object|null, history?: object[]}} data
 */
export function useViewerUnits({ units, job, truck, dashboard, history }) {
  const [settings] = useViewerSettings();
  return useMemo(
    () => applyViewerUnits({ units, job, truck, dashboard, history }, settings.units),
    [units, job, truck, dashboard, history, settings.units],
  );
}

export default useViewerUnits;