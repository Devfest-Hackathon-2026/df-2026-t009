import { useMemo, useRef, useState } from 'react'
import { distanceMeters, formatDistance } from '../geo'
import { timeAgo } from '../time'
import type { Drop } from '../types'
import type { Me } from '../useLocation'
import { PIN_GLYPH, pinState } from './pins'

/**
 * まわりの予告が1枚ずつ重なって流れるフィード。本文は伏せたまま。
 * 左右にスワイプ（またはボタン）でめくり、カードを押すと地図でその場所を見る。
 */
export function FeedView({
  drops,
  me,
  uid,
  openedIds,
  onPick,
  onAuthor,
}: {
  drops: Drop[]
  me: Me
  uid: string | undefined
  openedIds: Set<string>
  onPick: (id: string) => void
  onAuthor: (uid: string) => void
}) {
  const [sort, setSort] = useState<'new' | 'near'>('near')
  const [index, setIndex] = useState(0)
  const [dx, setDx] = useState(0)
  const start = useRef<number | null>(null)

  const items = useMemo(() => {
    const withDist = drops.map((d) => ({ d, dist: me.position ? distanceMeters(me.position, d) : null }))
    if (sort === 'near' && me.position) withDist.sort((a, b) => (a.dist ?? 0) - (b.dist ?? 0))
    return withDist
  }, [drops, me.position, sort])

  const i = Math.min(index, Math.max(0, items.length - 1))
  const go = (n: number) => setIndex(Math.max(0, Math.min(items.length - 1, i + n)))

  return (
    <div className="page feed">
      <header className="page__head">
        <h2>まわりの予告</h2>
        <div className="segmented segmented--small">
          <button className={sort === 'near' ? 'on' : ''} onClick={() => (setSort('near'), setIndex(0))}>
            近い順
          </button>
          <button className={sort === 'new' ? 'on' : ''} onClick={() => (setSort('new'), setIndex(0))}>
            新着
          </button>
        </div>
      </header>

      {items.length === 0 ? (
        <p className="empty">まだ投稿がありません。最初のビーコンを置いてみよう。</p>
      ) : (
        <>
          <div
            className="stack"
            onPointerDown={(e) => (start.current = e.clientX)}
            onPointerMove={(e) => start.current !== null && setDx(e.clientX - start.current)}
            onPointerUp={() => {
              if (Math.abs(dx) > 70) go(dx < 0 ? 1 : -1)
              start.current = null
              setDx(0)
            }}
            onPointerCancel={() => ((start.current = null), setDx(0))}
          >
            {items.slice(i, i + 3).map(({ d, dist }, k) => {
              const state = pinState(d, uid, openedIds, me.position, me.accuracy)
              const top = k === 0
              return (
                <article
                  key={d.id}
                  className={`card st-${state} ${top ? 'card--top' : ''}`}
                  style={{
                    zIndex: 10 - k,
                    transform: top
                      ? `translateX(${dx}px) rotate(${dx / 25}deg)`
                      : `translateY(${k * 14}px) scale(${1 - k * 0.05})`,
                    opacity: 1 - k * 0.25,
                  }}
                >
                  <div className="card__art">
                    <span className="card__orb">{PIN_GLYPH[state]}</span>
                    {d.hasPhoto && <span className="card__photo">📷 写真は現地で</span>}
                  </div>
                  <div className="card__body">
                    <div className="card__meta">
                      <button
                        className="author-link"
                        onClick={(e) => {
                          e.stopPropagation()
                          onAuthor(d.authorId)
                        }}
                      >
                        {d.authorName}
                      </button>
                      <span className="muted"> ・ {timeAgo(d.createdAt)}</span>
                    </div>
                    <h3>{d.teaser}</h3>
                    <div className="stats-row">
                      <span>👣 {d.openCount}</span>
                      <span>♥ {d.likeCount ?? 0}</span>
                      <span>💬 {d.commentCount ?? 0}</span>
                      {dist !== null && <span>📍 {formatDistance(dist)}</span>}
                    </div>
                    {top && (
                      <button className="btn btn--primary btn--wide" onClick={() => onPick(d.id)}>
                        地図で見る
                      </button>
                    )}
                  </div>
                </article>
              )
            })}
          </div>
          <div className="stack__nav">
            <button className="btn btn--ghost" disabled={i === 0} onClick={() => go(-1)}>
              ← 前
            </button>
            <span className="muted">
              {i + 1} / {items.length}
            </span>
            <button className="btn btn--ghost" disabled={i >= items.length - 1} onClick={() => go(1)}>
              次 →
            </button>
          </div>
        </>
      )}
    </div>
  )
}
