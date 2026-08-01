export function readableForeground(background: string) {
  const red = Number.parseInt(background.slice(1, 3), 16)
  const green = Number.parseInt(background.slice(3, 5), 16)
  const blue = Number.parseInt(background.slice(5, 7), 16)
  return (red * 299 + green * 587 + blue * 114) / 1000 >= 150
    ? '#111827'
    : '#ffffff'
}
