import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],

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