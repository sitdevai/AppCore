import type { TablePaginationConfig } from 'antd'

export const standardTablePagination: TablePaginationConfig = {
  defaultPageSize: 10,
  pageSizeOptions: ['10', '20', '50'],
  showSizeChanger: true,
  hideOnSinglePage: false,
  position: ['bottomRight'],
}

export const standardTableScroll = { x: 'max-content' } as const
