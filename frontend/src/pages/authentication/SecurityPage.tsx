import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { Alert, Button, Card, Form, Input, Space } from 'antd'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import {
  beginMfaEnrollment,
  changePassword,
  verifyMfaEnrollment,
} from '@/features/authentication/authApi'
import { PageHeader } from '@/shared/layout/PageHeader'

const passwordSchema = z.object({
  currentPassword: z.string().min(1).max(128),
  newPassword: z.string().min(15).max(128),
})
const enrollmentSchema = z.object({
  currentPassword: z.string().min(1).max(128),
})
const verificationSchema = z.object({
  code: z.string().regex(/^[0-9]{6}$/),
})

type PasswordValues = z.infer<typeof passwordSchema>
type EnrollmentValues = z.infer<typeof enrollmentSchema>
type VerificationValues = z.infer<typeof verificationSchema>

export function Component() {
  const { t } = useTranslation('auth')
  const passwordForm = useForm<PasswordValues>({
    resolver: zodResolver(passwordSchema),
  })
  const enrollmentForm = useForm<EnrollmentValues>({
    resolver: zodResolver(enrollmentSchema),
  })
  const verificationForm = useForm<VerificationValues>({
    resolver: zodResolver(verificationSchema),
  })
  const [manualKey, setManualKey] = useState<string>()
  const [authenticatorId, setAuthenticatorId] = useState<string>()
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>()
  const [message, setMessage] = useState<string>()

  const passwordMutation = useMutation({
    mutationFn: (values: PasswordValues) =>
      changePassword(values.currentPassword, values.newPassword),
    onSuccess: () => setMessage(t('passwordChanged')),
  })
  const enrollmentMutation = useMutation({
    mutationFn: ({ currentPassword }: EnrollmentValues) =>
      beginMfaEnrollment(currentPassword),
    onSuccess: (result) => {
      setManualKey(result.manualEntryKey)
      setAuthenticatorId(result.authenticatorId)
    },
  })
  const verificationMutation = useMutation({
    mutationFn: ({ code }: VerificationValues) => {
      if (!authenticatorId) throw new Error('Missing authenticator')
      return verifyMfaEnrollment(authenticatorId, code)
    },
    onSuccess: setRecoveryCodes,
  })
  const hasError =
    passwordMutation.isError ||
    enrollmentMutation.isError ||
    verificationMutation.isError

  return (
    <>
      <PageHeader title={t('securityTitle')} />
      <Space orientation="vertical" size="large" className="full-width">
        {message && (
          <Alert type="success" showIcon role="status" title={message} />
        )}
        {hasError && (
          <Alert
            type="error"
            showIcon
            role="alert"
            title={t('recoveryFailed')}
          />
        )}

        <div className="content-grid content-grid--two">
          <Card
            className="section-card"
            title={
              <span className="section-card__title">
                {t('changePasswordTitle')}
              </span>
            }
          >
            <Form
              layout="vertical"
              onFinish={() =>
                void passwordForm.handleSubmit((values) =>
                  passwordMutation.mutate(values),
                )()
              }
            >
              <Controller
                name="currentPassword"
                control={passwordForm.control}
                render={({ field, fieldState }) => (
                  <Form.Item
                    label={t('password')}
                    htmlFor="change-current-password"
                    validateStatus={fieldState.error ? 'error' : undefined}
                  >
                    <Input.Password
                      {...field}
                      id="change-current-password"
                      autoComplete="current-password"
                    />
                  </Form.Item>
                )}
              />
              <Controller
                name="newPassword"
                control={passwordForm.control}
                render={({ field, fieldState }) => (
                  <Form.Item
                    label={t('newPassword')}
                    htmlFor="change-new-password"
                    validateStatus={fieldState.error ? 'error' : undefined}
                  >
                    <Input.Password
                      {...field}
                      id="change-new-password"
                      autoComplete="new-password"
                    />
                  </Form.Item>
                )}
              />
              <div className="form-actions">
                <Button
                  type="primary"
                  htmlType="submit"
                  loading={passwordMutation.isPending}
                >
                  {t('changePasswordAction')}
                </Button>
              </div>
            </Form>
          </Card>

          <Card
            className="section-card"
            title={
              <span className="section-card__title">
                {t('mfaEnrollmentTitle')}
              </span>
            }
          >
            {!manualKey ? (
              <Form
                layout="vertical"
                onFinish={() =>
                  void enrollmentForm.handleSubmit((values) =>
                    enrollmentMutation.mutate(values),
                  )()
                }
              >
                <Controller
                  name="currentPassword"
                  control={enrollmentForm.control}
                  render={({ field, fieldState }) => (
                    <Form.Item
                      label={t('password')}
                      htmlFor="mfa-current-password"
                      validateStatus={fieldState.error ? 'error' : undefined}
                    >
                      <Input.Password
                        {...field}
                        id="mfa-current-password"
                        autoComplete="current-password"
                      />
                    </Form.Item>
                  )}
                />
                <div className="form-actions">
                  <Button
                    type="primary"
                    htmlType="submit"
                    loading={enrollmentMutation.isPending}
                  >
                    {t('beginMfaEnrollment')}
                  </Button>
                </div>
              </Form>
            ) : recoveryCodes ? (
              <Alert
                type="success"
                showIcon
                title={t('saveRecoveryCodes')}
                description={
                  <pre className="recovery-codes">
                    {recoveryCodes.join('\n')}
                  </pre>
                }
              />
            ) : (
              <Space orientation="vertical" size="large" className="full-width">
                <Alert
                  type="info"
                  showIcon
                  title={t('manualEntryKey')}
                  description={<code>{manualKey}</code>}
                />
                <Form
                  layout="vertical"
                  onFinish={() =>
                    void verificationForm.handleSubmit((values) =>
                      verificationMutation.mutate(values),
                    )()
                  }
                >
                  <Controller
                    name="code"
                    control={verificationForm.control}
                    render={({ field, fieldState }) => (
                      <Form.Item
                        label={t('mfaCode')}
                        htmlFor="mfa-enrollment-code"
                        validateStatus={fieldState.error ? 'error' : undefined}
                      >
                        <Input
                          {...field}
                          id="mfa-enrollment-code"
                          inputMode="numeric"
                          autoComplete="one-time-code"
                        />
                      </Form.Item>
                    )}
                  />
                  <div className="form-actions">
                    <Button
                      type="primary"
                      htmlType="submit"
                      loading={verificationMutation.isPending}
                    >
                      {t('verifyMfa')}
                    </Button>
                  </div>
                </Form>
              </Space>
            )}
          </Card>
        </div>
      </Space>
    </>
  )
}
