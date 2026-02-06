import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      input: {
        panel: resolve(__dirname, 'panel.html'),
        overlay: resolve(__dirname, 'overlay.html'),
        mobile: resolve(__dirname, 'mobile.html'),
        component: resolve(__dirname, 'component.html'),
        config: resolve(__dirname, 'config.html'),
      },
    },
  },
  server: {
    // Allow serving all HTML files during development
    open: '/panel.html',
  },
})
