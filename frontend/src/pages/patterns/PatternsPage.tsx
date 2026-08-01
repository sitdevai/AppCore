import { PlusOutlined } from '@ant-design/icons'
import { zodResolver } from '@hookform/resolvers/zod'
import { Button, Modal } from 'antd'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { EmptyState } from '@/shared/feedback/EmptyState'
import { ControlledTextInput } from '@/shared/forms/ControlledTextInput'
import { FormServerError } from '@/shared/forms/FormServerError'
import { ListPage } from '@/shared/patterns/ListPage'
import { PageHeader } from '@/shared/layout/PageHeader'

export function Component() {
  const { t } = useTranslation(['common', 'pages'])
  const [open, setOpen] = useState(false)
  const schema = z.object({
    sample: z.string().trim().min(1, t('common:requiredField')),
  })
  type SampleFormValues = z.infer<typeof schema>
  const {
    control,
    formState: { errors },
    handleSubmit,
    reset,
  } = useForm<SampleFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { sample: '' },
  })

  const close = () => {
    reset()
    setOpen(false)
  }

  return (
    <>
      <PageHeader
        title={t('pages:patternsTitle')}
        subtitle={t('pages:patternsSubtitle')}
        actions={
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setOpen(true)}
          >
            {t('pages:addSample')}
          </Button>
        }
      />
      <ListPage title={t('pages:sampleListTitle')}>
        <EmptyState />
      </ListPage>

      <Modal
        className="admin-modal"
        open={open}
        title={t('pages:addSample')}
        onCancel={close}
        footer={[
          <Button key="cancel" onClick={close}>
            {t('common:cancel')}
          </Button>,
          <Button
            key="save"
            type="primary"
            onClick={() =>
              void handleSubmit(() => {
                close()
              })()
            }
          >
            {t('common:save')}
          </Button>,
        ]}
      >
        <div className="modal-form">
          <FormServerError message={errors.root?.server?.message} />
          <ControlledTextInput
            control={control}
            name="sample"
            label={t('pages:sampleField')}
            required
            inputProps={{
              autoComplete: 'off',
              placeholder: t('pages:sampleFieldPlaceholder'),
            }}
          />
        </div>
      </Modal>
    </>
  )
}
