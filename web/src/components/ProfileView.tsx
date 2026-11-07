import { useEffect, useMemo, useState } from 'react'
import { clock, dayKey, dayLabel, timeAgo } from '../time'
import { levelOf, type Drop, type OpenedEntry, type PlacedEntry, type Profile } from '../types'

type LogItem = {
  id: string
  kind: 'opened' | 'placed'
  teaser: string
  sub: string
  at: { toDate(): Date } | null
}

function LevelCard({ name, profile, me }: { name: string; profile: Profile | null; me: boolean }) {
  const lv = levelOf(profile)
  return (
    <header className="profile-card">
      <div className="profile-card__avatar">{[...name][0] ?? '?'}</div>
      <div className="profile-card__main">
        <h2>
          {name}
          {me && <span className="muted small">（あなた）</span>}
        </h2>
        <div className="level">
          <span className="level__badge">Lv.{lv.level}</span>
          <div className="level__bar">
            <span style={{ width: `${Math.round(lv.progress * 100)}%` }} />
          </div>
          <span className="muted small">あと {lv.toNext}xp</span>
        </div>
      </div>
      <dl className="stat-grid">
        <div>
          <dt>ひらいた</dt>
          <dd>{profile?.opened ?? 0}</dd>
        </div>
        <div>
          <dt>置いた</dt>
          <dd>{profile?.placed ?? 0}</dd>
        </div>
        <div>
          <dt>ひらかれた</dt>
          <dd>{profile?.opensReceived ?? 0}</dd>
        </div>
        <div>
          <dt>♥ もらった</dt>
          <dd>{profile?.likesReceived ?? 0}</dd>
        </div>
      </dl>
    </header>
  )
}

/** 1日分のさんぽを、ストーリーのように1つずつ再生する。 */
function StoryPlayer({ label, items, onClose }: { label: string; items: LogItem[]; onClose: () => void }) {
  const [i, setI] = useState(0)
  useEffect(() => {
    const t = setTimeout(() => (i < items.length - 1 ? setI(i + 1) : onClose()), 2600)
    return () => clearTimeout(t)
  }, [i, items.length, onClose])
  const it = items[i]
  return (
    <div className="story" onClick={() => (i < items.length - 1 ? setI(i + 1) : onClose())}>
      <div className="story__bars">
        {items.map((_, k) => (
          <span key={k} className={k < i ? 'done' : k === i ? 'now' : ''} />
        ))}
      </div>
      <button className="story__close" onClick={onClose} aria-label="とじる">
        ×
      </button>
      <p className="story__day">{label}のさんぽ</p>
      <div key={it.id} className={`story__slide story__slide--${it.kind}`}>
        <span className="story__time">{clock(it.at)}</span>
        <span className="story__kind">{it.kind === 'opened' ? 'UNLOCK' : 'DROP'}</span>
        <h2>{it.teaser}</h2>
        <p>{it.sub}</p>
      </div>
      <p className="story__count">
        {i + 1} / {items.length}
      </p>
    </div>
  )
}

export function ProfileView({
  name,
  profile,
  isMe,
  opened,
  placed,
  theirDrops,
  onPick,
  onBack,
}: {
  name: string
  profile: Profile | null
  isMe: boolean
  opened: OpenedEntry[]
  placed: PlacedEntry[]
  theirDrops: Drop[]
  onPick: (id: string) => void
  onBack?: () => void
}) {
  const [playing, setPlaying] = useState<string | null>(null)

  const days = useMemo(() => {
    const items: LogItem[] = [
      ...opened.map((o) => ({
        id: `o-${o.id}`,
        kind: 'opened' as const,
        teaser: o.teaser,
        sub: `${o.authorName} の投稿を ${o.openedDistance}m 地点でひらいた`,
        at: o.openedAt,
      })),
      ...placed.map((p) => ({
        id: `p-${p.id}`,
        kind: 'placed' as const,
        teaser: p.teaser,
        sub: `半径 ${p.radius}m のビーコンを置いた`,
        at: p.createdAt,
      })),
    ]
    const map = new Map<string, LogItem[]>()
    for (const it of items) {
      const k = dayKey(it.at)
      map.set(k, [...(map.get(k) ?? []), it])
    }
    return [...map.entries()]
      .sort((a, b) => (a[0] < b[0] ? 1 : -1))
      .map(([k, list]) => ({
        key: k,
        label: dayLabel(k),
        items: list.sort((a, b) => (a.at?.toDate().getTime() ?? 0) - (b.at?.toDate().getTime() ?? 0)),
      }))
  }, [opened, placed])

  const playingDay = days.find((d) => d.key === playing)

  return (
    <div className="page profile">
      {onBack && (
        <button className="back" onClick={onBack}>
          ← もどる
        </button>
      )}
      <LevelCard name={name} profile={profile} me={isMe} />

      {isMe ? (
        <>
          <h3 className="section-title">さんぽログ</h3>
          {days.length === 0 ? (
            <p className="empty">まだ記録がありません。地図の「？」に近づいてひらいてみよう。</p>
          ) : (
            days.map((day) => (
              <section key={day.key} className="day">
                <header className="day__head">
                  <div>
                    <h4>{day.label}</h4>
                    <span className="muted small">
                      ひらいた {day.items.filter((x) => x.kind === 'opened').length} ・ 置いた{' '}
                      {day.items.filter((x) => x.kind === 'placed').length}
                    </span>
                  </div>
                  <button className="btn btn--primary btn--small" onClick={() => setPlaying(day.key)}>
                    ▶ ふりかえる
                  </button>
                </header>
                <div className="day__strip">
                  {day.items.map((it) => (
                    <button
                      key={it.id}
                      className={`tile tile--${it.kind}`}
                      onClick={() => onPick(it.id.slice(2))}
                    >
                      <span className="tile__time">{clock(it.at)}</span>
                      <span className="tile__kind">{it.kind === 'opened' ? 'ひらいた' : '置いた'}</span>
                      <span className="tile__teaser">{it.teaser}</span>
                    </button>
                  ))}
                </div>
              </section>
            ))
          )}
        </>
      ) : (
        <>
          <h3 className="section-title">{name} が置いたビーコン</h3>
          {theirDrops.length === 0 ? (
            <p className="empty">まだ投稿がありません。</p>
          ) : (
            <ul className="list">
              {theirDrops.map((d) => (
                <li key={d.id}>
                  <button onClick={() => onPick(d.id)}>
                    <span className="list__title">{d.teaser}</span>
                    <span className="muted small">
                      {timeAgo(d.createdAt)} ・ 👣 {d.openCount} ・ ♥ {d.likeCount ?? 0} ・ 💬 {d.commentCount ?? 0}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </>
      )}

      {playingDay && <StoryPlayer label={playingDay.label} items={playingDay.items} onClose={() => setPlaying(null)} />}
    </div>
  )
}
