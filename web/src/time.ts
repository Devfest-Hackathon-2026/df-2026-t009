type Ts = { toDate(): Date } | null | undefined

export function toDate(ts: Ts): Date | null {
  return ts ? ts.toDate() : null
}

export function timeAgo(ts: Ts) {
  const d = toDate(ts)
  if (!d) return 'たった今'
  const s = Math.max(0, (Date.now() - d.getTime()) / 1000)
  if (s < 60) return 'たった今'
  if (s < 3600) return `${Math.floor(s / 60)}分前`
  if (s < 86400) return `${Math.floor(s / 3600)}時間前`
  if (s < 86400 * 7) return `${Math.floor(s / 86400)}日前`
  return `${d.getMonth() + 1}/${d.getDate()}`
}

export function clock(ts: Ts) {
  const d = toDate(ts)
  if (!d) return ''
  return `${d.getHours()}:${String(d.getMinutes()).padStart(2, '0')}`
}

/** 1日は午前4時から翌午前4時まで（夜のおでかけを同じ日にまとめる）。 */
export function dayKey(ts: Ts) {
  const d = toDate(ts) ?? new Date()
  const shifted = new Date(d.getTime() - 4 * 3600 * 1000)
  return `${shifted.getFullYear()}-${String(shifted.getMonth() + 1).padStart(2, '0')}-${String(shifted.getDate()).padStart(2, '0')}`
}

export function dayLabel(key: string) {
  const today = dayKey(null)
  const y = dayKey({ toDate: () => new Date(Date.now() - 86400 * 1000) })
  if (key === today) return '今日'
  if (key === y) return '昨日'
  const [, m, d] = key.split('-')
  return `${Number(m)}月${Number(d)}日`
}
