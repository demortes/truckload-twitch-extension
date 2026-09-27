import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

const root = import.meta.dirname

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Twitch serves the built assets from its own CDN path, not from the domain root,
  // so asset URLs must be relative.
  base: './',
  build: {
    rollupOptions: {
      input: {
        panel: resolve(root, 'panel.html'),
        overlay: resolve(root, 'overlay.html'),
        mobile: resolve(root, 'mobile.html'),
        component: resolve(root, 'component.html'),
        config: resolve(root, 'config.html'),
      },
    },
  },
  server: {
    // Allow serving all HTML files during development
    open: '/panel.html',
  },
})
