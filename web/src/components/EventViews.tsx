import { useMemo, useState } from 'react'
import { distanceMeters, formatDistance } from '../geo'
import { timeAgo } from '../time'
import type { Drop, KokoEvent } from '../types'
import type { Me } from '../useLocation'

type Ts = { toDate(): Date } | null

function fmtDate(ts: Ts) {
  if (!ts) return ''
  const d = ts.toDate()
  return `${d.getMonth() + 1}/${d.getDate()} ${d.getHours()}:${String(d.getMinutes()).padStart(2, '0')}`
}

export function eventStatus(ev: KokoEvent): 'upcoming' | 'live' | 'ended' {
  const now = Date.now()
  if (ev.startAt && ev.startAt.toDate().getTime() > now) return 'upcoming'
  if (ev.endAt && ev.endAt.toDate().getTime() < now) return 'ended'
  return 'live'
}

const STATUS_LABEL = { upcoming: 'これから', live: '開催中', ended: '終了' }

function Period({ ev }: { ev: KokoEvent }) {
  if (!ev.startAt && !ev.endAt) return <span className="muted small">いつでも</span>
  return (
    <span className="muted small">
      {fmtDate(ev.startAt) || '〜'} {ev.startAt && ev.endAt ? '〜' : ''} {fmtDate(ev.endAt)}
    </span>
  )
}

function Progress({ done, total }: { done: number; total: number }) {
  return (
    <div className="ev-progress">
      <div className="ev-progress__bar">
        <span style={{ width: `${(done / total) * 100}%` }} />
      </div>
      <span className="ev-progress__num">
        {done}/{total}
      </span>
    </div>
  )
}

export function EventList({
  events,
  joinedIds,
  openedIds,
  onOpen,
  onCreate,
}: {
  events: KokoEvent[]
  joinedIds: Set<string>
  openedIds: Set<string>
  onOpen: (id: string) => void
  onCreate: () => void
}) {
  const sorted = [...events].sort((a, b) => {
    const rank = (e: KokoEvent) => (joinedIds.has(e.id) ? 0 : eventStatus(e) === 'live' ? 1 : eventStatus(e) === 'upcoming' ? 2 : 3)
    return rank(a) - rank(b)
  })
  return (
    <>
      <button className="ev-create" onClick={onCreate}>
        <span className="ev-create__plus">＋</span>
        <span>
          <b>イベントを作る</b>
          <small>投稿をつないで、巡るルートやスタンプラリーに</small>
        </span>
      </button>
      {sorted.length === 0 ? (
        <p className="empty">まだイベントはありません。</p>
      ) : (
        <ul className="ev-list">
          {sorted.map((ev) => {
            const st = eventStatus(ev)
            const done = ev.stops.filter((s) => openedIds.has(s.id)).length
            return (
              <li key={ev.id}>
                <button onClick={() => onOpen(ev.id)} className={`ev-card ev-card--${st}`}>
                  <div className="ev-card__head">
                    <span className={`ev-badge ev-badge--${st}`}>{STATUS_LABEL[st]}</span>
                    {joinedIds.has(ev.id) && <span className="ev-badge ev-badge--joined">参加中</span>}
                    <span className="muted small">{ev.stops.length}か所 ・ {ev.participantCount}人</span>
                  </div>
                  <h3>{ev.title}</h3>
                  <Period ev={ev} />
                  {joinedIds.has(ev.id) && <Progress done={done} total={ev.stops.length} />}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </>
  )
}

export function EventDetail({
  ev,
  me,
  uid,
  joined,
  openedIds,
  busy,
  onJoin,
  onShowRoute,
  onPickStop,
  onAuthor,
  onBack,
}: {
  ev: KokoEvent
  me: Me
  uid: string | undefined
  joined: boolean
  openedIds: Set<string>
  busy: boolean
  onJoin: (join: boolean) => void
  onShowRoute: () => void
  onPickStop: (id: string) => void
  onAuthor: (uid: string) => void
  onBack: () => void
}) {
  const st = eventStatus(ev)
  const done = ev.stops.filter((s) => openedIds.has(s.id)).length
  const total = ev.stops.reduce(
    (m, s, i) => (i === 0 ? 0 : m + distanceMeters(ev.stops[i - 1], s)),
    0,
  )
  return (
    <div className="page">
      <button className="back" onClick={onBack}>
        ← もどる
      </button>
      <header className="ev-hero">
        <span className={`ev-badge ev-badge--${st}`}>{STATUS_LABEL[st]}</span>
        <h2>{ev.title}</h2>
        <p className="muted small">
          <button className="author-link" onClick={() => onAuthor(ev.authorId)}>
            {ev.authorName}
          </button>{' '}
          が作成 ・ {timeAgo(ev.createdAt)} ・ {ev.participantCount}人が参加
        </p>
        <Period ev={ev} />
        {ev.description && <p className="ev-hero__desc">{ev.description}</p>}
        <div className="ev-hero__stats">
          <div>
            <b>{ev.stops.length}</b>か所
          </div>
          <div>
            <b>{formatDistance(total)}</b>ルート
          </div>
          <div>
            <b>
              {done}/{ev.stops.length}
            </b>
            ひらいた
          </div>
        </div>
        <Progress done={done} total={ev.stops.length} />
        <div className="ev-hero__actions">
          <button className="btn btn--primary btn--glow" onClick={onShowRoute}>
            地図でルートを見る
          </button>
          {ev.authorId !== uid && (
            <button className="btn btn--ghost" disabled={busy || st === 'ended'} onClick={() => onJoin(!joined)}>
              {joined ? '参加をやめる' : '参加する'}
            </button>
          )}
        </div>
      </header>

      <h3 className="section-title">巡る順</h3>
      <ol className="ev-stops">
        {ev.stops.map((s, i) => {
          const opened = openedIds.has(s.id)
          const dist = me.position ? distanceMeters(me.position, s) : null
          return (
            <li key={s.id} className={opened ? 'done' : ''}>
              <button onClick={() => onPickStop(s.id)}>
                <span className="ev-stops__num">{opened ? '✓' : i + 1}</span>
                <span className="ev-stops__text">
                  <b>{s.teaser}</b>
                  <small className="muted">
                    {opened ? 'ひらいた' : `半径 ${s.radius >= 1000 ? `${s.radius / 1000}km` : `${s.radius}m`}`}
                    {dist !== null && ` ・ ここから ${formatDistance(dist)}`}
                  </small>
                </span>
              </button>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

export type EventDraft = { title: string; description: string; dropIds: string[]; startAt?: number; endAt?: number }

export function EventCreate({
  drops,
  me,
  busy,
  error,
  onSubmit,
  onCancel,
}: {
  drops: Drop[]
  me: Me
  busy: boolean
  error: string | null
  onSubmit: (d: EventDraft) => void
  onCancel: () => void
}) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [picked, setPicked] = useState<string[]>([])

  const candidates = useMemo(() => {
    const list = drops.map((d) => ({ d, dist: me.position ? distanceMeters(me.position, d) : null }))
    if (me.position) list.sort((a, b) => (a.dist ?? 0) - (b.dist ?? 0))
    return list.slice(0, 80)
  }, [drops, me.position])

  const toggle = (id: string) =>
    setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : p.length >= 20 ? p : [...p, id]))

  const valid = title.trim().length > 0 && picked.length >= 2

  return (
    <div className="page">
      <button className="back" onClick={onCancel}>
        ← やめる
      </button>
      <h2 className="page-title">イベントを作る</h2>
      <p className="muted small">投稿を巡る順にタップして、ルートやスタンプラリーにしよう。中身はそれぞれ、近づいた人だけが読めます。</p>

      <label className="field">
        <span>イベント名</span>
        <input value={title} maxLength={40} placeholder="例：学園祭 こっそり巡り" onChange={(e) => setTitle(e.target.value)} />
      </label>
      <label className="field">
        <span>説明（任意）</span>
        <textarea
          value={description}
          maxLength={300}
          rows={2}
          placeholder="例：正門から始めて、最後は大隈講堂の裏へ。"
          onChange={(e) => setDescription(e.target.value)}
        />
      </label>
      <div className="field field--row">
        <label>
          <span>はじまり（任意）</span>
          <input type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} />
        </label>
        <label>
          <span>おわり（任意）</span>
          <input type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} />
        </label>
      </div>

      <div className="field">
        <span>巡る場所（{picked.length}/20、2か所以上）</span>
        {candidates.length < 2 ? (
          <p className="muted small">まだ投稿が少ないので、先に地図で投稿を置いてみよう。</p>
        ) : (
          <ul className="ev-pick">
            {candidates.map(({ d, dist }) => {
              const n = picked.indexOf(d.id)
              return (
                <li key={d.id}>
                  <button type="button" className={n >= 0 ? 'on' : ''} onClick={() => toggle(d.id)}>
                    <span className="ev-pick__num">{n >= 0 ? n + 1 : ''}</span>
                    <span className="ev-pick__text">
                      <b>{d.teaser}</b>
                      <small className="muted">
                        {d.authorName}
                        {dist !== null && ` ・ ${formatDistance(dist)}`}
                      </small>
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>
        )}
      </div>

      {error && <p className="error">{error}</p>}
      <div className="sticky-actions">
        <button
          className="btn btn--primary btn--wide btn--glow"
          disabled={!valid || busy}
          onClick={() =>
            onSubmit({
              title,
              description,
              dropIds: picked,
              startAt: start ? new Date(start).getTime() : undefined,
              endAt: end ? new Date(end).getTime() : undefined,
            })
          }
        >
          {busy ? '作っています…' : `${picked.length}か所のイベントを作る`}
        </button>
      </div>
    </div>
  )
}
