import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/api': {
          target: env.API_TARGET,
          changeOrigin: true,

          // Accept the local ASP.NET Core development certificate.
          secure: false,
        },
      },
    },
  }
})
