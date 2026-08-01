import { Card, Form } from 'antd'
import type { FormEventHandler, PropsWithChildren, ReactNode } from 'react'

interface FormSectionProps extends PropsWithChildren {
  title: ReactNode
  onSubmit: FormEventHandler<HTMLFormElement>
}

export function FormSection({ title, children, onSubmit }: FormSectionProps) {
  return (
    <Card
      className="section-card form-section"
      title={<span className="section-card__title">{title}</span>}
    >
      <Form layout="vertical" requiredMark="optional" component={false}>
        <form noValidate onSubmit={onSubmit}>
          <div className="form-section__body">{children}</div>
        </form>
      </Form>
    </Card>
  )
}
