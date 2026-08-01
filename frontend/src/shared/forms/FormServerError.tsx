import { Alert } from 'antd'
import { useEffect, useRef } from 'react'

interface FormServerErrorProps {
  message?: string
}

export function FormServerError({ message }: FormServerErrorProps) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (message) {
      containerRef.current?.focus()
    }
  }, [message])

  if (!message) return null

  return (
    <div className="form-server-error" ref={containerRef} tabIndex={-1}>
      <Alert type="error" showIcon role="alert" title={message} />
    </div>
  )
}
