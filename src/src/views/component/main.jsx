import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import ComponentApp from './ComponentApp';
import '../../index.css';

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <ComponentApp />
  </StrictMode>
);
