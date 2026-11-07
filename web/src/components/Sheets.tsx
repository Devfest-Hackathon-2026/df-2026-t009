import { useEffect, useRef, useState } from 'react'
import { distanceMeters, formatDistance, isWithin, RADII, type LatLng } from '../geo'
import { timeAgo } from '../time'
import type { Comment, Drop } from '../types'
import type { Me } from '../useLocation'
import { PixelUnlock } from './Pixel'
import { PIN_GLYPH, pinState } from './pins'

export function DropSheet({
  drop,
  me,
  uid,
  opened,
  busy,
  error,
  onOpen,
  onClose,
  onAuthor,
}: {
  drop: Drop
  me: Me
  uid: string | undefined
  opened: boolean
  busy: boolean
  error: string | null
  onOpen: () => void
  onClose: () => void
  onAuthor: (uid: string) => void
}) {
  const dist = me.position ? distanceMeters(me.position, drop) : null
  const mine = drop.authorId === uid
  const inRange = dist !== null && isWithin(dist, drop.radius, me.accuracy)
  const canOpen = mine || opened || inRange
  const remaining = dist !== null ? Math.max(0, dist - drop.radius) : null
  const state = pinState(drop, uid, opened ? new Set([drop.id]) : new Set(), me.position, me.accuracy)
  // 範囲の外 1km までをゲージで表す
  const gauge = dist === null ? 0 : canOpen ? 1 : Math.max(0.04, 1 - (dist - drop.radius) / 1000)

  return (
    <section className={`sheet sheet--drop st-${state}`} role="dialog" aria-label="投稿の予告">
      <button className="sheet__close" onClick={onClose} aria-label="閉じる">
        ×
      </button>
      <div className="drop-head">
        <span className="drop-head__orb">
          <span>{PIN_GLYPH[state]}</span>
        </span>
        <div>
          <button className="author-link" onClick={() => onAuthor(drop.authorId)}>
            {drop.authorName}
          </button>
          <span className="muted"> ・ {timeAgo(drop.createdAt)}</span>
          <h2 className="sheet__teaser">{drop.teaser}</h2>
        </div>
      </div>
      <div className="stats-row">
        <span>👣 {drop.openCount}</span>
        <span>♥ {drop.likeCount ?? 0}</span>
        <span>💬 {drop.commentCount ?? 0}</span>
        <span>◎ 半径 {drop.radius}m</span>
        {drop.hasPhoto && <span>📷</span>}
      </div>

      {!canOpen && (
        <div className="gauge">
          <div className="gauge__bar">
            <span style={{ width: `${gauge * 100}%` }} />
          </div>
          <p>
            {remaining === null ? '現在地が分かると、あとどれくらいか表示されます' : <>あと <b>{formatDistance(remaining)}</b> 近づくとひらける</>}
          </p>
        </div>
      )}
      {error && <p className="error">{error}</p>}
      {canOpen ? (
        <button className="btn btn--primary btn--wide btn--glow" disabled={busy} onClick={onOpen}>
          {busy ? 'ひらいています…' : mine ? '自分の投稿を見る' : opened ? 'もう一度読む' : 'ひらく'}
        </button>
      ) : (
        <button className="btn btn--locked btn--wide" disabled>
          🔒 近づくとひらけます
        </button>
      )}
    </section>
  )
}

export function OpenedView({
  drop,
  body,
  photo,
  firstTime,
  mine,
  liked,
  comments,
  onLike,
  onComment,
  onAuthor,
  onClose,
  onDelete,
}: {
  drop: Drop
  body: string
  photo: string | null
  firstTime: boolean
  mine: boolean
  liked: boolean
  comments: Comment[]
  onLike: () => void
  onComment: (text: string) => Promise<boolean>
  onAuthor: (uid: string) => void
  onClose: () => void
  onDelete: () => void
}) {
  const [burst, setBurst] = useState(firstTime)
  const [text, setText] = useState('')
  const [sending, setSending] = useState(false)
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: 'nearest' })
  }, [comments.length])

  if (burst) return <PixelUnlock onDone={() => setBurst(false)} />

  return (
    <div className="overlay" onClick={onClose}>
      <article className="letter" onClick={(e) => e.stopPropagation()}>
        {firstTime && <div className="stamp">ここで ひらいた</div>}
        <button className="author-link" onClick={() => onAuthor(drop.authorId)}>
          {drop.authorName}
        </button>
        <span className="muted"> ・ {timeAgo(drop.createdAt)}</span>
        <h2 className="letter__teaser">{drop.teaser}</h2>
        {photo && <img className="letter__photo" src={photo} alt="投稿者がその場で撮った写真" />}
        {body && <p className="letter__body">{body}</p>}

        <div className="react-row">
          <button className={`like ${liked ? 'like--on' : ''}`} onClick={onLike} aria-pressed={liked}>
            {liked ? '♥' : '♡'} {drop.likeCount ?? 0}
          </button>
          <span className="muted">👣 {drop.openCount}人がこの場所でひらいた</span>
        </div>

        <section className="guestbook">
          <h3>
            ここに来た人の寄せ書き <span className="muted">（ひらいた人だけが見られます）</span>
          </h3>
          {comments.length === 0 ? (
            <p className="muted small">まだ誰も書いていません。最初のひとことを残そう。</p>
          ) : (
            <ul>
              {comments.map((c) => (
                <li key={c.id}>
                  <button className="author-link" onClick={() => onAuthor(c.authorId)}>
                    {c.authorName}
                  </button>
                  <span className="muted small"> {timeAgo(c.createdAt)}</span>
                  <p>{c.text}</p>
                </li>
              ))}
            </ul>
          )}
          <div ref={endRef} />
          <form
            className="guestbook__form"
            onSubmit={async (e) => {
              e.preventDefault()
              if (!text.trim() || sending) return
              setSending(true)
              if (await onComment(text)) setText('')
              setSending(false)
            }}
          >
            <input value={text} maxLength={200} placeholder="ひとこと残す" onChange={(e) => setText(e.target.value)} />
            <button className="btn btn--primary" disabled={!text.trim() || sending}>
              送る
            </button>
          </form>
        </section>

        <div className="letter__actions">
          {mine && (
            <button className="btn btn--ghost" onClick={onDelete}>
              この投稿を消す
            </button>
          )}
          <button className="btn btn--primary" onClick={onClose}>
            とじる
          </button>
        </div>
      </article>
    </div>
  )
}

export type PostDraft = { teaser: string; body: string; radius: number; photo: File | null }

export function PostSheet({
  at,
  me,
  busy,
  error,
  onSubmit,
  onCancel,
}: {
  at: LatLng
  me: Me
  busy: boolean
  error: string | null
  onSubmit: (d: PostDraft) => void
  onCancel: () => void
}) {
  const [teaser, setTeaser] = useState('')
  const [body, setBody] = useState('')
  const [radius, setRadius] = useState<number>(100)
  const [photo, setPhoto] = useState<File | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const nearMe = me.position ? distanceMeters(me.position, at) < 30 : false

  const pick = (f: File | null) => {
    setPhoto(f)
    if (preview) URL.revokeObjectURL(preview)
    setPreview(f ? URL.createObjectURL(f) : null)
  }

  const valid = teaser.trim().length > 0 && (body.trim().length > 0 || photo)

  return (
    <div className="overlay overlay--bottom">
      <form
        className="sheet sheet--form"
        onSubmit={(e) => {
          e.preventDefault()
          if (valid && !busy) onSubmit({ teaser, body, radius, photo })
        }}
      >
        <h2 className="sheet__title">ここにビーコンを置く</h2>
        {nearMe && (
          <p className="notice">
            今いる場所にそのまま置こうとしています。自宅や職場など、知られたくない場所には置かないでください。
          </p>
        )}

        <div className="field">
          <span>いまの景色（その場で撮る・加工なし）</span>
          {preview ? (
            <div className="photo-pick">
              <img src={preview} alt="撮った写真" />
              <button type="button" className="btn btn--ghost" onClick={() => pick(null)}>
                撮り直す
              </button>
            </div>
          ) : (
            <label className="shutter">
              <span className="shutter__btn" aria-hidden />
              <span>タップして撮る</span>
              <input
                type="file"
                accept="image/*"
                capture="environment"
                hidden
                onChange={(e) => pick(e.target.files?.[0] ?? null)}
              />
            </label>
          )}
        </div>

        <label className="field">
          <span>予告（誰でも見える）</span>
          <input
            value={teaser}
            maxLength={60}
            placeholder="例：この角度から見ると、鳥に見える建物"
            onChange={(e) => setTeaser(e.target.value)}
          />
          <small>{[...teaser].length}/60</small>
        </label>

        <label className="field">
          <span>本文（近づいた人だけ読める）</span>
          <textarea
            value={body}
            maxLength={600}
            rows={3}
            placeholder="正門を背にして、左の塔を見上げてみて。"
            onChange={(e) => setBody(e.target.value)}
          />
          <small>{[...body].length}/600</small>
        </label>

        <div className="field">
          <span>どこまで近づいたらひらけるか</span>
          <div className="segmented" role="radiogroup">
            {RADII.map((r) => (
              <button
                type="button"
                key={r}
                role="radio"
                aria-checked={radius === r}
                className={radius === r ? 'on' : ''}
                onClick={() => setRadius(r)}
              >
                {r >= 1000 ? `${r / 1000}km` : `${r}m`}
              </button>
            ))}
          </div>
        </div>

        {error && <p className="error">{error}</p>}
        <div className="sheet__actions">
          <button type="button" className="btn btn--ghost" onClick={onCancel} disabled={busy}>
            やめる
          </button>
          <button type="submit" className="btn btn--primary btn--glow" disabled={!valid || busy}>
            {busy ? '置いています…' : '置く'}
          </button>
        </div>
      </form>
    </div>
  )
}
