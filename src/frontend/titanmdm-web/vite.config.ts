import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],

  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },

  server: {
    host: '0.0.0.0',
    port: 3020,
    strictPort: true,

    proxy: {
      '/api': {
        target: 'http://localhost:8020',
        changeOrigin: true,
        secure: false,
      },

      '/hubs': {
        target: 'http://localhost:8020',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },

  preview: {
    host: '0.0.0.0',
    port: 3020,
    strictPort: true,
  },
})