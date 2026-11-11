import { createRoot } from 'react-dom/client';
import DevBanner from './DevBanner';

/** Mounts the dev status banner on its own root, next to (not inside) the view's React tree. */
export function mountDevBanner() {
  const host = document.createElement('div');
  host.id = 'dev-banner-root';
  document.body.appendChild(host);
  createRoot(host).render(<DevBanner />);
}