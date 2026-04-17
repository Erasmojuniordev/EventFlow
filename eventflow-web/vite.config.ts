import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(), // Tailwind v4: plugin integrado ao Vite, sem tailwind.config.js
  ],
  server: {
    port: 5173,
    // Proxy para evitar problemas de CORS em desenvolvimento
    // Todas as chamadas para /api são redirecionadas para o backend
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
