import axios from 'axios'
import { normalizeApiError } from '@/lib/api/errors'

const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ??
  (import.meta.env.MODE === 'test' ? 'http://localhost' : undefined)

if (!apiBaseUrl) {
  throw new Error('VITE_API_BASE_URL must be configured.')
}

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
  timeout: 15_000,
  headers: { Accept: 'application/json' },
  withCredentials: true,
})

apiClient.interceptors.request.use((configuration) => {
  configuration.headers.set('X-Correlation-ID', crypto.randomUUID())
  return configuration
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => Promise.reject(normalizeApiError(error)),
)
