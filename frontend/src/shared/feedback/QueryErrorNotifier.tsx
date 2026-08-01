import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { queryErrorEventName } from '@/lib/queryClient'
import type { ErrorHistoryRecord } from '@/lib/errorHistory'

export function QueryErrorNotifier() {
  const navigate = useNavigate()

  useEffect(() => {
    const showErrorPage = (event: Event) => {
      const detail = (event as CustomEvent<ErrorHistoryRecord>).detail
      const parameters = new URLSearchParams({
        code: detail.code,
        errorNumber: detail.errorNumber,
        occurredAtUtc: detail.occurredAtUtc,
      })
      if (detail.status) parameters.set('status', String(detail.status))
      if (detail.correlationId) {
        parameters.set('correlationId', detail.correlationId)
      }
      void navigate(`/error?${parameters.toString()}`, { replace: true })
    }
    window.addEventListener(queryErrorEventName, showErrorPage)
    return () => window.removeEventListener(queryErrorEventName, showErrorPage)
  }, [navigate])

  return null
}
