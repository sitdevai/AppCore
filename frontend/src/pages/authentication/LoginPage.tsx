import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Card, Form, Input, Space, Typography } from 'antd'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import {
  bootstrapAuthenticationFlow,
  completeMfa,
  login,
  type AuthenticationFlowContext,
} from '@/features/authentication/authApi'
import { useBranding } from '@/features/branding/useBranding'

const schema = z.object({
  username: z.string().trim().min(1),
  password: z.string().min(1).max(128),
})

const mfaSchema = z.object({
  code: z.string().regex(/^[0-9]{6}$/),
})

type LoginValues = z.infer<typeof schema>
type MfaValues = z.infer<typeof mfaSchema>

export function Component() {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const branding = useBranding()
  const [challengeId, setChallengeId] = useState<string>()
  const [csrf, setCsrf] = useState<AuthenticationFlowContext>()
  const [error, setError] = useState(false)
  const loginForm = useForm<LoginValues>({ resolver: zodResolver(schema) })
  const mfaForm = useForm<MfaValues>({ resolver: zodResolver(mfaSchema) })

  const submitLogin = loginForm.handleSubmit(async (values) => {
    setError(false)
    try {
      const tokens = await bootstrapAuthenticationFlow()
      const result = await login(values.username, values.password, tokens)
      if (result.status === 'mfaRequired' && result.mfaChallengeId) {
        setCsrf(tokens)
        setChallengeId(result.mfaChallengeId)
        return
      }
      if (result.status === 'authenticated') {
        await navigate('/')
        return
      }
      if (result.status === 'recoveryRequired') {
        await navigate('/recovery', {
          state: { username: values.username },
        })
        return
      }
      setError(true)
    } catch {
      setError(true)
    }
  })

  const submitMfa = mfaForm.handleSubmit(async ({ code }) => {
    if (!challengeId || !csrf) return
    setError(false)
    try {
      const result = await completeMfa(challengeId, code, csrf)
      if (result.status === 'authenticated') {
        await navigate('/')
        return
      }
      setError(true)
    } catch {
      setError(true)
    }
  })

  return (
    <main className="auth-page">
      <Card className="auth-card">
        <Space orientation="vertical" size="large" className="full-width">
          <div className="auth-brand">
            {branding.lightLogoUrl && (
              <img
                src={branding.lightLogoUrl}
                alt={branding.organizationName}
              />
            )}
            <Typography.Text strong>
              {branding.organizationName}
            </Typography.Text>
          </div>
          <div>
            <Typography.Title level={2}>{t('loginTitle')}</Typography.Title>
            <Typography.Text type="secondary">
              {t('loginDescription')}
            </Typography.Text>
          </div>
          {error && (
            <Alert
              type="error"
              showIcon
              role="alert"
              title={t('invalidCredentials')}
            />
          )}
          {!challengeId ? (
            <Form layout="vertical" onFinish={() => void submitLogin()}>
              <Controller
                name="username"
                control={loginForm.control}
                render={({ field, fieldState }) => (
                  <Form.Item
                    htmlFor="login-username"
                    label={t('username')}
                    validateStatus={fieldState.error ? 'error' : undefined}
                    help={
                      fieldState.error
                        ? t('required', { ns: 'validation' })
                        : undefined
                    }
                  >
                    <Input
                      {...field}
                      id="login-username"
                      autoComplete="username"
                      autoFocus
                    />
                  </Form.Item>
                )}
              />
              <Controller
                name="password"
                control={loginForm.control}
                render={({ field, fieldState }) => (
                  <Form.Item
                    htmlFor="login-password"
                    label={t('password')}
                    validateStatus={fieldState.error ? 'error' : undefined}
                    help={
                      fieldState.error
                        ? t('required', { ns: 'validation' })
                        : undefined
                    }
                  >
                    <Input.Password
                      {...field}
                      id="login-password"
                      autoComplete="current-password"
                    />
                  </Form.Item>
                )}
              />
              <Button
                type="primary"
                htmlType="submit"
                block
                loading={loginForm.formState.isSubmitting}
              >
                {t('loginAction')}
              </Button>
            </Form>
          ) : (
            <Form layout="vertical" onFinish={() => void submitMfa()}>
              <Controller
                name="code"
                control={mfaForm.control}
                render={({ field, fieldState }) => (
                  <Form.Item
                    htmlFor="login-mfa-code"
                    label={t('mfaCode')}
                    validateStatus={fieldState.error ? 'error' : undefined}
                    help={fieldState.error ? t('invalidMfaCode') : undefined}
                  >
                    <Input
                      {...field}
                      id="login-mfa-code"
                      inputMode="numeric"
                      autoComplete="one-time-code"
                      maxLength={6}
                      autoFocus
                    />
                  </Form.Item>
                )}
              />
              <Button
                type="primary"
                htmlType="submit"
                block
                loading={mfaForm.formState.isSubmitting}
              >
                {t('verifyMfa')}
              </Button>
            </Form>
          )}
          <Link to="/activation">{t('activateAccount')}</Link>
        </Space>
      </Card>
    </main>
  )
}
