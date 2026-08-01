const textCollator = new Intl.Collator(['ar', 'en'], {
  numeric: true,
  sensitivity: 'base',
})

export function compareTableText(left?: string | null, right?: string | null) {
  return textCollator.compare(left ?? '', right ?? '')
}

export function compareTableDate(left?: string | null, right?: string | null) {
  const leftValue = left ? Date.parse(left) : 0
  const rightValue = right ? Date.parse(right) : 0
  return leftValue - rightValue
}

export function compareTableBoolean(left: boolean, right: boolean) {
  return Number(left) - Number(right)
}

export function compareTableNumber(left: number, right: number) {
  return left - right
}
