import { useState } from 'react'
import type { OpenedEntry, PlacedEntry } from '../types'

function fmt(ts: { toDate(): Date } | null) {
  if (!ts) return ''
  const d = ts.toDate()
  return `${d.getMonth() + 1}/${d.getDate()} ${d.getHours()}:${String(d.getMinutes()).padStart(2, '0')}`
}

export function LogView({
  name,
  opened,
  placed,
  onPick,
}: {
  name: string
  opened: OpenedEntry[]
  placed: PlacedEntry[]
  onPick: (id: string) => void
}) {
  const [tab, setTab] = useState<'opened' | 'placed'>('opened')
  const items = tab === 'opened' ? opened : placed
  return (
    <div className="log">
      <header className="log__head">
        <h2>{name} の散歩ログ</h2>
        <p>
          ひらいた {opened.length} ・ おいた {placed.length}
        </p>
      </header>
      <div className="segmented segmented--tabs">
        <button className={tab === 'opened' ? 'on' : ''} onClick={() => setTab('opened')}>
          ひらいた
        </button>
        <button className={tab === 'placed' ? 'on' : ''} onClick={() => setTab('placed')}>
          おいた
        </button>
      </div>
      {items.length === 0 ? (
        <p className="empty">
          {tab === 'opened'
            ? 'まだ何もひらいていません。地図の「？」に近づいてみよう。'
            : 'まだ何も置いていません。気になった場所に投稿を置いてみよう。'}
        </p>
      ) : (
        <ol className="log__list">
          {items.map((it) => (
            <li key={it.id}>
              <button onClick={() => onPick(it.id)}>
                <span className="log__time">
                  {fmt('openedAt' in it ? it.openedAt : (it as PlacedEntry).createdAt)}
                </span>
                <span className="log__teaser">{it.teaser}</span>
                <span className="log__sub">
                  {'authorName' in it ? `${it.authorName} ・ ${it.openedDistance}m 地点でひらいた` : `半径 ${it.radius}m`}
                </span>
              </button>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}
