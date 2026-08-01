import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Card, Form, Input, Space, Typography } from 'antd'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router-dom'
import { z } from 'zod'
import {
  bootstrapAuthenticationFlow,
  completeChallenge,
} from '@/features/authentication/authApi'

const schema = z
  .object({
    username: z.string().trim().min(1),
    code: z.string().length(22),
    newPassword: z.string().min(15).max(128),
    confirmPassword: z.string().min(15).max(128),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
  })

type Values = z.infer<typeof schema>

export function Component() {
  const { t } = useTranslation('auth')
  const location = useLocation()
  const safePurpose =
    location.pathname === '/password-reset' ? 'password-reset' : 'activation'
  const [completed, setCompleted] = useState(false)
  const [error, setError] = useState(false)
  const form = useForm<Values>({ resolver: zodResolver(schema) })

  const submit = form.handleSubmit(async (values) => {
    setError(false)
    try {
      const csrf = await bootstrapAuthenticationFlow()
      await completeChallenge(safePurpose, values, csrf)
      setCompleted(true)
    } catch {
      setError(true)
    }
  })

  return (
    <main className="auth-page">
      <Card className="auth-card">
        <Space orientation="vertical" size="large" className="full-width">
          <Typography.Title level={2}>
            {safePurpose === 'activation'
              ? t('activationTitle')
              : t('resetTitle')}
          </Typography.Title>
          {completed ? (
            <Alert
              type="success"
              showIcon
              role="status"
              title={t('challengeCompleted')}
            />
          ) : (
            <>
              {error && (
                <Alert
                  type="error"
                  showIcon
                  role="alert"
                  title={t('invalidChallenge')}
                />
              )}
              <Form layout="vertical" onFinish={() => void submit()}>
                {(
                  [
                    'username',
                    'code',
                    'newPassword',
                    'confirmPassword',
                  ] as const
                ).map((name) => (
                  <Controller
                    key={name}
                    name={name}
                    control={form.control}
                    render={({ field, fieldState }) => (
                      <Form.Item
                        htmlFor={`challenge-${name}`}
                        label={t(name)}
                        validateStatus={fieldState.error ? 'error' : undefined}
                        help={
                          fieldState.error
                            ? t('invalid', { ns: 'validation' })
                            : undefined
                        }
                      >
                        {name.includes('Password') ? (
                          <Input.Password
                            {...field}
                            id={`challenge-${name}`}
                            autoComplete="new-password"
                          />
                        ) : (
                          <Input
                            {...field}
                            id={`challenge-${name}`}
                            autoComplete="off"
                          />
                        )}
                      </Form.Item>
                    )}
                  />
                ))}
                <Button
                  type="primary"
                  htmlType="submit"
                  block
                  loading={form.formState.isSubmitting}
                >
                  {t('confirm', { ns: 'common' })}
                </Button>
              </Form>
            </>
          )}
          <Link to="/login">{t('backToLogin')}</Link>
        </Space>
      </Card>
    </main>
  )
}
