import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Configuration Vite : en développement local (npm run dev), les appels
// vers /api sont redirigés vers le backend .NET pour éviter les soucis de CORS.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 3000,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET || 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
