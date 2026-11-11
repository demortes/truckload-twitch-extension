import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import OverlayApp from './OverlayApp';
import '../../index.css';

// Dev server only: a status strip showing mock vs live data. The import is dropped from production builds.
if (import.meta.env.DEV) import('../../dev/mountDevBanner').then((m) => m.mountDevBanner());

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <OverlayApp />
  </StrictMode>
);
