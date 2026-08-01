import { SaveOutlined, UndoOutlined, UploadOutlined } from '@ant-design/icons'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Card, Form, Input, Select, Space, Typography } from 'antd'
import { useEffect, useRef } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import {
  defaultBranding,
  getBranding,
  restoreBrandingDefaults,
  updateBranding,
  uploadBrandingAsset,
  type Branding,
} from '@/features/branding/brandingApi'
import { getCurrentUser } from '@/features/authentication/authApi'
import { readableForeground } from '@/features/branding/brandingColors'
import {
  hasPermission,
  permissions,
} from '@/features/authorization/permissions'
import { ConfirmAction } from '@/shared/feedback/ConfirmAction'
import { FormServerError } from '@/shared/forms/FormServerError'
import { applyApiValidationErrors } from '@/shared/forms/formErrors'
import { formMutationMeta } from '@/shared/forms/formMutation'
import { PageHeader } from '@/shared/layout/PageHeader'

const schema = z.object({
  organizationName: z.string().trim().min(1).max(200),
  shortOrganizationName: z.string().trim().min(1).max(80),
  primaryColor: z.string().regex(/^#[0-9a-fA-F]{6}$/),
  secondaryColor: z.string().regex(/^#[0-9a-fA-F]{6}$/),
  headerColor: z.string().regex(/^#[0-9a-fA-F]{6}$/),
  backgroundColor: z.string().regex(/^#[0-9a-fA-F]{6}$/),
  patternColor: z.string().regex(/^#[0-9a-fA-F]{6}$/),
  backgroundPattern: z.enum(['None', 'Dots', 'Grid', 'Diagonal', 'Geometric']),
  version: z.number(),
})

type Values = z.infer<typeof schema>

export function Component() {
  const { t } = useTranslation(['settings', 'common'])
  const client = useQueryClient()
  const branding = useQuery({ queryKey: ['branding'], queryFn: getBranding })
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
  })
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: defaultBranding,
  })
  useEffect(() => {
    if (branding.data) form.reset(branding.data)
  }, [branding.data, form])

  const refresh = (value: Branding) => {
    client.setQueryData(['branding'], value)
    form.reset(value)
  }
  const save = useMutation({
    mutationFn: (value: Values) => updateBranding(value as Branding),
    meta: formMutationMeta,
    onSuccess: refresh,
    onError: (error) =>
      applyApiValidationErrors(error, form.setError, {
        organizationName: 'organizationName',
        shortOrganizationName: 'shortOrganizationName',
        primaryColor: 'primaryColor',
        secondaryColor: 'secondaryColor',
        headerColor: 'headerColor',
        backgroundColor: 'backgroundColor',
        patternColor: 'patternColor',
        backgroundPattern: 'backgroundPattern',
      }),
  })
  const asset = useMutation({
    mutationFn: ({ kind, file }: { kind: string; file: File }) =>
      uploadBrandingAsset(kind, file),
    onSuccess: refresh,
  })
  const restore = useMutation({
    mutationFn: restoreBrandingDefaults,
    onSuccess: refresh,
  })
  const canUpdate = hasPermission(
    currentUser.data,
    permissions.settingsVisualIdentityUpdate,
  )
  const values = useWatch({ control: form.control })
  const assetInputs = useRef<Partial<Record<string, HTMLInputElement | null>>>(
    {},
  )

  return (
    <>
      <PageHeader
        title={t('title')}
        subtitle={t('subtitle')}
        actions={
          canUpdate ? (
            <Space wrap>
              <ConfirmAction
                onConfirm={() =>
                  void form.handleSubmit((value) => save.mutate(value))()
                }
              >
                <Button
                  type="primary"
                  icon={<SaveOutlined />}
                  loading={save.isPending}
                >
                  {t('save')}
                </Button>
              </ConfirmAction>
              <ConfirmAction onConfirm={() => restore.mutate()}>
                <Button
                  danger
                  icon={<UndoOutlined />}
                  loading={restore.isPending}
                >
                  {t('restore')}
                </Button>
              </ConfirmAction>
            </Space>
          ) : undefined
        }
      />

      <Space orientation="vertical" size="large" className="full-width">
        <Card className="section-card">
          <form
            onSubmit={(event) => {
              void form.handleSubmit((value) => save.mutate(value))(event)
            }}
          >
            <div className="form-section__body">
              <FormServerError
                message={form.formState.errors.root?.server?.message}
              />
              {(['organizationName', 'shortOrganizationName'] as const).map(
                (name) => (
                  <Controller
                    key={name}
                    name={name}
                    control={form.control}
                    render={({ field, fieldState }) => (
                      <Form.Item
                        label={t(name)}
                        validateStatus={fieldState.error ? 'error' : undefined}
                        help={fieldState.error?.message}
                      >
                        <Input
                          {...field}
                          disabled={!canUpdate}
                          status={fieldState.error ? 'error' : undefined}
                        />
                      </Form.Item>
                    )}
                  />
                ),
              )}
              {(
                [
                  'primaryColor',
                  'secondaryColor',
                  'headerColor',
                  'backgroundColor',
                  'patternColor',
                ] as const
              ).map((name) => (
                <Controller
                  key={name}
                  name={name}
                  control={form.control}
                  render={({ field, fieldState }) => (
                    <Form.Item
                      label={t(name)}
                      validateStatus={fieldState.error ? 'error' : undefined}
                      help={fieldState.error?.message}
                    >
                      <Input
                        {...field}
                        type="color"
                        disabled={!canUpdate}
                        aria-label={t(name)}
                      />
                    </Form.Item>
                  )}
                />
              ))}
              <Controller
                name="backgroundPattern"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Form.Item
                    label={t('backgroundPattern')}
                    validateStatus={fieldState.error ? 'error' : undefined}
                    help={fieldState.error?.message}
                  >
                    <Select
                      {...field}
                      disabled={!canUpdate}
                      options={(
                        [
                          'None',
                          'Dots',
                          'Grid',
                          'Diagonal',
                          'Geometric',
                        ] as const
                      ).map((pattern) => ({
                        value: pattern,
                        label: t(`backgroundPatterns.${pattern}`),
                      }))}
                    />
                  </Form.Item>
                )}
              />
            </div>
          </form>
        </Card>

        {canUpdate && (
          <Card
            className="section-card"
            title={<span className="section-card__title">{t('assets')}</span>}
          >
            <div className="asset-upload-grid">
              {(
                ['LightLogo', 'DarkLogo', 'CompactLogo', 'Favicon'] as const
              ).map((kind) => {
                const assetUrl = {
                  LightLogo: branding.data?.lightLogoUrl,
                  DarkLogo: branding.data?.darkLogoUrl,
                  CompactLogo: branding.data?.compactLogoUrl,
                  Favicon: branding.data?.faviconUrl,
                }[kind]

                return (
                  <div className="asset-upload" key={kind}>
                    <Typography.Text strong>{t(kind)}</Typography.Text>
                    {assetUrl && (
                      <div
                        className={`asset-upload__preview-surface${
                          kind === 'DarkLogo'
                            ? ' asset-upload__preview-surface--dark'
                            : ''
                        }`}
                      >
                        <img
                          className={`asset-upload__preview${
                            kind === 'Favicon'
                              ? ' asset-upload__preview--favicon'
                              : ''
                          }`}
                          src={assetUrl}
                          alt={t(kind)}
                        />
                      </div>
                    )}
                    <Button
                      icon={<UploadOutlined />}
                      loading={asset.isPending}
                      onClick={() => assetInputs.current[kind]?.click()}
                    >
                      {assetUrl ? t('common:replace') : t('common:add')}
                    </Button>
                    <input
                      hidden
                      ref={(element) => {
                        assetInputs.current[kind] = element
                      }}
                      type="file"
                      accept={
                        kind === 'Favicon'
                          ? '.png,.ico'
                          : '.png,.jpg,.jpeg,.webp'
                      }
                      onChange={(event) => {
                        const file = event.target.files?.[0]
                        if (file) asset.mutate({ kind, file })
                        event.target.value = ''
                      }}
                    />
                  </div>
                )
              })}
            </div>
          </Card>
        )}

        <Card
          title={<span className="section-card__title">{t('preview')}</span>}
          className="section-card branding-preview"
          style={
            {
              '--brand-secondary': values.secondaryColor,
            } as React.CSSProperties
          }
        >
          <div
            className={`branding-preview__canvas branding-preview__canvas--${(
              values.backgroundPattern ?? 'None'
            ).toLowerCase()}`}
            style={
              {
                '--preview-header': values.headerColor,
                '--preview-header-foreground': readableForeground(
                  values.headerColor ?? defaultBranding.headerColor,
                ),
                '--preview-background': values.backgroundColor,
                '--preview-pattern': values.patternColor,
              } as React.CSSProperties
            }
          >
            <div className="branding-preview__header">
              {branding.data?.compactLogoUrl && (
                <img
                  src={branding.data.compactLogoUrl}
                  alt=""
                  className="branding-preview__compact-logo"
                />
              )}
              <Typography.Text strong>
                {values.organizationName}
              </Typography.Text>
            </div>
            <div className="branding-preview__content">
              <div className="branding-preview__variants">
                <div className="branding-preview__variant">
                  <Typography.Text>{t('LightLogo')}</Typography.Text>
                  <div className="branding-preview__surface">
                    {branding.data?.lightLogoUrl && (
                      <img
                        className="branding-preview__logo"
                        src={branding.data.lightLogoUrl}
                        alt={`${t('LightLogo')} — ${t('preview')}`}
                      />
                    )}
                  </div>
                </div>
                <div className="branding-preview__variant">
                  <Typography.Text>{t('DarkLogo')}</Typography.Text>
                  <div className="branding-preview__surface branding-preview__surface--dark">
                    {branding.data?.darkLogoUrl && (
                      <img
                        className="branding-preview__logo"
                        src={branding.data.darkLogoUrl}
                        alt={`${t('DarkLogo')} — ${t('preview')}`}
                      />
                    )}
                  </div>
                </div>
              </div>
              <div className="branding-preview__names">
                <Typography.Title level={3}>
                  {values.organizationName}
                </Typography.Title>
                <Typography.Text style={{ color: values.primaryColor }}>
                  {values.shortOrganizationName}
                </Typography.Text>
              </div>
            </div>
          </div>
        </Card>
      </Space>
    </>
  )
}
