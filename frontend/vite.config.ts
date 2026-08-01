import { defineConfig } from 'vitest/config'
import { loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, process.cwd(), '')
  const httpsPfxPath =
    process.env.DEV_HTTPS_PFX_PATH ?? environment.DEV_HTTPS_PFX_PATH
  const httpsPfxPassword =
    process.env.DEV_HTTPS_PFX_PASSWORD ?? environment.DEV_HTTPS_PFX_PASSWORD

  return {
    plugins: [react()],
    server: {
      ...(httpsPfxPath && httpsPfxPassword
        ? {
            https: {
              pfx: readFileSync(httpsPfxPath),
              passphrase: httpsPfxPassword,
            },
          }
        : {}),
      proxy: {
        '/api': {
          target: 'https://localhost:7080',
          changeOrigin: true,
          secure: false,
        },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
    },
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
  }
})
