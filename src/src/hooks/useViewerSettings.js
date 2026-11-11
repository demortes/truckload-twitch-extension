import { useEffect, useSyncExternalStore } from 'react';
import { STORAGE_KEY, viewerSettingsStore } from '../services/viewerSettings';

/**
 * The viewer's own display preferences, and a setter that takes a partial change.
 * Changes made in another view of the same extension (panel, overlay, video component share one origin)
 * arrive through the browser's `storage` event, so they follow each other live.
 */
export function useViewerSettings() {
  useEffect(() => {
    const onStorage = (event) => {
      if (event.key === null || event.key === STORAGE_KEY) viewerSettingsStore.reload();
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const settings = useSyncExternalStore(viewerSettingsStore.subscribe, viewerSettingsStore.get, viewerSettingsStore.get);
  return [settings, viewerSettingsStore.set];
}

export default useViewerSettings;