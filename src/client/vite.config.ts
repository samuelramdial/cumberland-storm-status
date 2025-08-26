import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5052', // your API base
        changeOrigin: true,
        // secure: false   // only needed if using https
      },
    },
  },
})
