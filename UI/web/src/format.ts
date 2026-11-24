/**
 * Clip.Host sends the stored local timestamp ("yyyy-MM-ddTHH:mm:ss.fff").
 * Today shows a clock, yesterday its word, the rest of the week a weekday,
 * older rows a short date — the design-language list meta grammar.
 */
export type TimeFormat = '24h' | '12h'

const pad = (value: number) => String(value).padStart(2, '0')

function clock(when: Date, timeFormat: TimeFormat): string {
  const minutes = pad(when.getMinutes())
  if (timeFormat === '12h') {
    const hours = when.getHours() % 12 || 12
    return `${pad(hours)}:${minutes} ${when.getHours() < 12 ? 'AM' : 'PM'}`
  }
  return `${pad(when.getHours())}:${minutes}`
}

export function formatTime(value: string, timeFormat: TimeFormat = '24h', now = new Date()): string {
  if (!value) return ''
  const when = new Date(value)
  if (Number.isNaN(when.getTime())) return value

  const startOfDay = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()
  const days = Math.round((startOfDay(now) - startOfDay(when)) / 86400000)

  if (days === 0) return clock(when, timeFormat)
  if (days === 1) return 'Yesterday'
  if (days > 1 && days < 7) return when.toLocaleDateString('en-GB', { weekday: 'short' })

  const date = when.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
  return when.getFullYear() === now.getFullYear() ? date : `${date} ${when.getFullYear()}`
}
