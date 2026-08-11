import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// SlotDemo.Server hosts the REST API on :5090 in dev, and serves this
// SPA's build output (web/dist) statically in production.
const SERVER_ORIGIN = 'http://localhost:5090'

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': { target: SERVER_ORIGIN, changeOrigin: true },
    },
  },
})
