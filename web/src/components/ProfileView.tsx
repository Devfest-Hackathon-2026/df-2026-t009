import { useEffect, useMemo, useState } from 'react'
import { clock, dayKey, dayLabel, timeAgo } from '../time'
import { formatDistance } from '../geo'
import { levelOf, type Drop, type OpenedEntry, type PlacedEntry, type Profile } from '../types'
import { trailLength, type Trails } from '../useTrail'
import { TrailSketch } from './TrailSketch'
import { CrystalFigure, type AvatarLook } from './Crystal'
import { LookPicker } from './Welcome'

type LogItem = {
  id: string
  kind: 'opened' | 'placed'
  teaser: string
  sub: string
  at: { toDate(): Date } | null
}

function LevelCard({ name, profile, me, look }: { name: string; profile: Profile | null; me: boolean; look?: AvatarLook }) {
  const lv = levelOf(profile)
  return (
    <header className="profile-card">
      {look ? (
        <div className="profile-card__figure">
          <CrystalFigure look={look} size={96} />
        </div>
      ) : (
        <div className="profile-card__avatar">{[...name][0] ?? '?'}</div>
      )}
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

/** 1日分のおでかけを、ストーリーのように1つずつ再生する。 */
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
      <p className="story__day">{label}のおでかけ</p>
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
  trails = {},
  share = false,
  onShare,
  onClearTrails,
  look,
  onLook,
  theme,
  onTheme,
  onPick,
  onBack,
}: {
  name: string
  profile: Profile | null
  isMe: boolean
  opened: OpenedEntry[]
  placed: PlacedEntry[]
  theirDrops: Drop[]
  trails?: Trails
  share?: boolean
  onShare?: (v: boolean) => void
  onClearTrails?: () => void
  look?: AvatarLook
  onLook?: (l: AvatarLook) => void
  theme?: 'holo' | 'milk'
  onTheme?: (t: 'holo' | 'milk') => void
  onPick: (id: string) => void
  onBack?: () => void
}) {
  const [playing, setPlaying] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)

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
        sub: `半径 ${p.radius >= 1000 ? `${p.radius / 1000}km` : `${p.radius}m`} の投稿を置いた`,
        at: p.createdAt,
      })),
    ]
    const map = new Map<string, LogItem[]>()
    for (const it of items) {
      const k = dayKey(it.at)
      map.set(k, [...(map.get(k) ?? []), it])
    }
    for (const k of Object.keys(trails)) if (!map.has(k) && trails[k].length > 1) map.set(k, [])
    return [...map.entries()]
      .sort((a, b) => (a[0] < b[0] ? 1 : -1))
      .map(([k, list]) => ({
        key: k,
        label: dayLabel(k),
        trail: trails[k] ?? [],
        items: list.sort((a, b) => (a.at?.toDate().getTime() ?? 0) - (b.at?.toDate().getTime() ?? 0)),
      }))
  }, [opened, placed, trails])

  const playingDay = days.find((d) => d.key === playing)

  return (
    <div className="page profile">
      {onBack && (
        <button className="back" onClick={onBack}>
          ← もどる
        </button>
      )}
      <LevelCard name={name} profile={profile} me={isMe} look={isMe ? look : undefined} />
      {isMe && look && onLook && (
        <div className="profile-actions">
          <button className="btn btn--ghost btn--small" onClick={() => setEditing(!editing)}>
            {editing ? '人形の設定をとじる' : '人形を変える'}
          </button>
          {onTheme && (
            <button className="btn btn--ghost btn--small" onClick={() => onTheme(theme === 'milk' ? 'holo' : 'milk')}>
              表示：{theme === 'milk' ? 'MILK（明るい）' : 'HOLO（暗い）'}
            </button>
          )}
        </div>
      )}
      {isMe && editing && look && onLook && <LookPicker look={look} onChange={onLook} />}

      {isMe ? (
        <>
          <h3 className="section-title">おでかけログ</h3>
          {days.length === 0 ? (
            <p className="empty">まだ記録がありません。出かけて、地図の「？」に近づいてひらいてみよう。</p>
          ) : (
            days.map((day) => (
              <section key={day.key} className="day">
                <header className="day__head">
                  <div>
                    <h4>{day.label}</h4>
                    <span className="muted small">
                      {formatDistance(trailLength(day.trail))} 移動 ・ ひらいた {day.items.filter((x) => x.kind === "opened").length} ・ 置いた{" "}
                      {day.items.filter((x) => x.kind === "placed").length}
                    </span>
                  </div>
                  <button className="btn btn--primary btn--small" disabled={day.items.length === 0} onClick={() => setPlaying(day.key)}>
                    ▶ ふりかえる
                  </button>
                </header>
                <div className="day__strip">
                  {day.trail.length > 1 && <TrailSketch points={day.trail} />}
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

          <h3 className="section-title">足あとの設定</h3>
          <div className="settings">
            <label className="toggle">
              <span>
                <b>みんなの踏み跡に加える</b>
                <small className="muted">
                  移動した場所を、約30m四方のマス目ごとの回数としてだけ共有します。誰が通ったかは残りません。自宅のまわりなどでは、オフのままにしてください。
                </small>
              </span>
              <input type="checkbox" checked={share} onChange={(e) => onShare?.(e.target.checked)} />
            </label>
            <button className="btn btn--ghost btn--small" onClick={() => onClearTrails?.()}>
              この端末の足あとを消す
            </button>
          </div>
        </>
      ) : (
        <>
          <h3 className="section-title">{name} が置いた投稿</h3>
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
