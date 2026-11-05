import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],

  build: {
    /*
     * Las paginas usan React.lazy (bundle inicial ~360 KB). El unico chunk
     * mayor a 500 KB es ExcelJS (~930 KB), que se descarga solo al exportar.
     */
    chunkSizeWarningLimit: 1000,
  },

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