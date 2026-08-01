import { Form, Input } from 'antd'
import type { InputProps } from 'antd'
import { Controller } from 'react-hook-form'
import type { Control, FieldValues, Path } from 'react-hook-form'
import { useId, type ReactNode } from 'react'

interface ControlledTextInputProps<TFields extends FieldValues> {
  control: Control<TFields>
  name: Path<TFields>
  label: ReactNode
  required?: boolean
  inputProps?: InputProps
}

export function ControlledTextInput<TFields extends FieldValues>({
  control,
  name,
  label,
  required = false,
  inputProps,
}: ControlledTextInputProps<TFields>) {
  const instanceId = useId()
  const inputId = `${name}-${instanceId}`
  const errorId = `${inputId}-error`

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => {
        return (
          <Form.Item
            label={label}
            htmlFor={inputId}
            required={required}
            validateStatus={fieldState.error ? 'error' : undefined}
            help={
              fieldState.error ? (
                <span id={errorId} role="alert">
                  {fieldState.error.message}
                </span>
              ) : undefined
            }
          >
            <Input
              {...inputProps}
              {...field}
              id={inputId}
              aria-invalid={fieldState.invalid}
              aria-required={required}
              aria-describedby={fieldState.error ? errorId : undefined}
            />
          </Form.Item>
        )
      }}
    />
  )
}
