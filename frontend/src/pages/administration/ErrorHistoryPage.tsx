import { DatePicker, Input, Space, Table, Tag, Typography } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { readErrorHistory } from '@/lib/errorHistory'
import { PageHeader } from '@/shared/layout/PageHeader'
import { ListPage } from '@/shared/patterns/ListPage'
import {
  standardTablePagination,
  standardTableScroll,
} from '@/shared/tables/tableDefaults'
import {
  compareTableDate,
  compareTableNumber,
  compareTableText,
} from '@/shared/tables/tableSorting'

export function Component() {
  const { i18n } = useTranslation()
  const isArabic = i18n.language.startsWith('ar')
  const label = (arabic: string, english: string) =>
    isArabic ? arabic : english
  const [errorNumber, setErrorNumber] = useState('')
  const [dates, setDates] = useState<[Dayjs | null, Dayjs | null] | null>(null)
  const records = useMemo(() => {
    const number = errorNumber.trim().toLowerCase()
    return readErrorHistory().filter((record) => {
      const occurredAt = new Date(record.occurredAtUtc).getTime()
      return (
        (!number || record.errorNumber.toLowerCase().includes(number)) &&
        (!dates?.[0] || occurredAt >= dates[0].startOf('day').valueOf()) &&
        (!dates?.[1] || occurredAt <= dates[1].endOf('day').valueOf())
      )
    })
  }, [dates, errorNumber])

  return (
    <>
      <PageHeader
        title={label('سجل الأخطاء', 'Error history')}
        subtitle={label(
          'البحث في أخطاء الواجهة حسب التاريخ أو رقم الخطأ.',
          'Search client errors by date or error number.',
        )}
      />
      <ListPage
        title={label('الأخطاء المسجلة', 'Recorded errors')}
        toolbar={
          <Space wrap>
            <Input.Search
              allowClear
              placeholder={label('رقم الخطأ', 'Error number')}
              value={errorNumber}
              onChange={(event) => setErrorNumber(event.target.value)}
              style={{ width: 280 }}
            />
            <DatePicker.RangePicker
              value={dates}
              onChange={setDates}
              placeholder={
                isArabic ? ['من تاريخ', 'إلى تاريخ'] : ['From date', 'To date']
              }
            />
          </Space>
        }
      >
        <Table
          className="app-data-grid"
          rowKey="errorNumber"
          dataSource={records}
          pagination={standardTablePagination}
          scroll={standardTableScroll}
          columns={[
            {
              title: label('رقم الخطأ', 'Error number'),
              dataIndex: 'errorNumber',
              fixed: 'start',
              sorter: (left, right) =>
                compareTableText(left.errorNumber, right.errorNumber),
              render: (value: string) => (
                <Typography.Text copyable code>
                  {value}
                </Typography.Text>
              ),
            },
            {
              title: label('التاريخ', 'Date'),
              dataIndex: 'occurredAtUtc',
              defaultSortOrder: 'descend',
              sorter: (left, right) =>
                compareTableDate(left.occurredAtUtc, right.occurredAtUtc),
              render: (value: string) =>
                new Intl.DateTimeFormat(i18n.language, {
                  dateStyle: 'medium',
                  timeStyle: 'medium',
                }).format(new Date(value)),
            },
            {
              title: label('رمز الخطأ', 'Error code'),
              dataIndex: 'code',
              sorter: (left, right) => compareTableText(left.code, right.code),
              render: (value: string) => <Tag color="error">{value}</Tag>,
            },
            {
              title: 'HTTP',
              dataIndex: 'status',
              sorter: (left, right) =>
                compareTableNumber(left.status ?? 0, right.status ?? 0),
              render: (value?: number) => value ?? '—',
            },
            {
              title: label('معرف الارتباط', 'Correlation ID'),
              dataIndex: 'correlationId',
              sorter: (left, right) =>
                compareTableText(left.correlationId, right.correlationId),
              render: (value?: string) =>
                value ? (
                  <Typography.Text copyable code>
                    {value}
                  </Typography.Text>
                ) : (
                  '—'
                ),
            },
          ]}
        />
      </ListPage>
    </>
  )
}
