import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Card, Form, Input, Space, Typography } from 'antd'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router-dom'
import { z } from 'zod'
import {
  beginMfaEnrollment,
  beginRecovery,
  bootstrapAuthenticationFlow,
  verifyMfaEnrollment,
} from '@/features/authentication/authApi'

const recoverySchema = z.object({
  username: z.string().trim().min(1),
  password: z.string().min(1).max(128),
  recoveryCode: z.string().length(22),
})
const verificationSchema = z.object({
  code: z.string().regex(/^[0-9]{6}$/),
})
type RecoveryValues = z.infer<typeof recoverySchema>
type VerificationValues = z.infer<typeof verificationSchema>

export function Component() {
  const { t } = useTranslation('auth')
  const location = useLocation()
  const locationState: unknown = location.state
  const parsedLocationState = z
    .object({ username: z.string() })
    .safeParse(locationState)
  const suggestedUsername = parsedLocationState.success
    ? parsedLocationState.data.username
    : ''
  const recoveryForm = useForm<RecoveryValues>({
    resolver: zodResolver(recoverySchema),
    defaultValues: { username: suggestedUsername },
  })
  const verificationForm = useForm<VerificationValues>({
    resolver: zodResolver(verificationSchema),
  })
  const [manualKey, setManualKey] = useState<string>()
  const [authenticatorId, setAuthenticatorId] = useState<string>()
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>()
  const [error, setError] = useState(false)

  const resumeEnrollment = async () => {
    setError(false)
    try {
      const enrollment = await beginMfaEnrollment(undefined, true)
      setManualKey(enrollment.manualEntryKey)
      setAuthenticatorId(enrollment.authenticatorId)
    } catch {
      setError(true)
    }
  }

  const recover = recoveryForm.handleSubmit(async (values) => {
    setError(false)
    try {
      const csrf = await bootstrapAuthenticationFlow()
      await beginRecovery(values, csrf)
      const enrollment = await beginMfaEnrollment(undefined, true)
      setManualKey(enrollment.manualEntryKey)
      setAuthenticatorId(enrollment.authenticatorId)
    } catch {
      setError(true)
    }
  })
  const verify = verificationForm.handleSubmit(async ({ code }) => {
    setError(false)
    try {
      if (!authenticatorId) throw new Error('Missing authenticator')
      setRecoveryCodes(await verifyMfaEnrollment(authenticatorId, code, true))
    } catch {
      setError(true)
    }
  })

  return (
    <main className="auth-page">
      <Card className="auth-card">
        <Space orientation="vertical" size="large" className="full-width">
          <Typography.Title level={2}>{t('recoveryTitle')}</Typography.Title>
          {error && (
            <Alert
              type="error"
              showIcon
              role="alert"
              title={t('recoveryFailed')}
            />
          )}
          {!manualKey ? (
            <Form layout="vertical" onFinish={() => void recover()}>
              {(['username', 'password', 'recoveryCode'] as const).map(
                (name) => (
                  <Controller
                    key={name}
                    name={name}
                    control={recoveryForm.control}
                    render={({ field, fieldState }) => (
                      <Form.Item
                        label={t(name)}
                        htmlFor={`recovery-${name}`}
                        validateStatus={fieldState.error ? 'error' : undefined}
                        help={
                          fieldState.error
                            ? t('invalid', { ns: 'validation' })
                            : undefined
                        }
                      >
                        {name === 'password' ? (
                          <Input.Password
                            {...field}
                            id={`recovery-${name}`}
                            autoComplete="current-password"
                          />
                        ) : (
                          <Input
                            {...field}
                            id={`recovery-${name}`}
                            autoComplete="off"
                          />
                        )}
                      </Form.Item>
                    )}
                  />
                ),
              )}
              <Button block type="primary" htmlType="submit">
                {t('startRecovery')}
              </Button>
              <Button block onClick={() => void resumeEnrollment()}>
                {t('resumeRecovery')}
              </Button>
            </Form>
          ) : recoveryCodes ? (
            <Alert
              type="success"
              showIcon
              role="status"
              title={t('saveRecoveryCodes')}
              description={
                <pre className="recovery-codes">{recoveryCodes.join('\n')}</pre>
              }
            />
          ) : (
            <>
              <Alert
                type="info"
                showIcon
                title={t('manualEntryKey')}
                description={<code>{manualKey}</code>}
              />
              <Form layout="vertical" onFinish={() => void verify()}>
                <Controller
                  name="code"
                  control={verificationForm.control}
                  render={({ field, fieldState }) => (
                    <Form.Item
                      label={t('mfaCode')}
                      htmlFor="recovery-mfa-code"
                      validateStatus={fieldState.error ? 'error' : undefined}
                    >
                      <Input
                        {...field}
                        id="recovery-mfa-code"
                        inputMode="numeric"
                        autoComplete="one-time-code"
                      />
                    </Form.Item>
                  )}
                />
                <Button block type="primary" htmlType="submit">
                  {t('verifyMfa')}
                </Button>
              </Form>
            </>
          )}
        </Space>
      </Card>
    </main>
  )
}
