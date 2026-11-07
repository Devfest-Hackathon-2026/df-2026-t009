import { useState } from 'react'
import { distanceMeters, formatDistance, isWithin, RADII, type LatLng } from '../geo'
import type { Drop } from '../types'
import type { Me } from '../useLocation'

export function DropSheet({
  drop,
  me,
  uid,
  opened,
  busy,
  error,
  onOpen,
  onClose,
}: {
  drop: Drop
  me: Me
  uid: string | undefined
  opened: boolean
  busy: boolean
  error: string | null
  onOpen: () => void
  onClose: () => void
}) {
  const dist = me.position ? distanceMeters(me.position, drop) : null
  const mine = drop.authorId === uid
  const inRange = dist !== null && isWithin(dist, drop.radius, me.accuracy)
  const canOpen = mine || opened || inRange
  const remaining = dist !== null ? Math.max(0, dist - drop.radius) : null

  return (
    <section className="sheet" role="dialog" aria-label="投稿の予告">
      <button className="sheet__close" onClick={onClose} aria-label="閉じる">
        ×
      </button>
      <p className="sheet__meta">
        {drop.authorName} が置いた{drop.hasPhoto ? '写真つきの' : ''}投稿 ・ {drop.openCount}人がひらいた
      </p>
      <h2 className="sheet__teaser">{drop.teaser}</h2>
      <div className="sheet__range">
        <span className="chip">半径 {drop.radius}m 以内でひらける</span>
        {dist !== null && <span className="chip chip--soft">ここから {formatDistance(dist)}</span>}
      </div>
      {error && <p className="error">{error}</p>}
      {canOpen ? (
        <button className="btn btn--primary btn--wide" disabled={busy} onClick={onOpen}>
          {busy ? 'ひらいています…' : mine ? '自分の投稿を見る' : opened ? 'もう一度読む' : 'ひらく'}
        </button>
      ) : (
        <div className="locked">
          <div className="locked__icon" aria-hidden>
            ?
          </div>
          <p>
            {remaining === null
              ? '現在地が分かると、あとどれくらいか表示されます'
              : `あと ${formatDistance(remaining)} 近づくとひらけます`}
          </p>
        </div>
      )}
    </section>
  )
}

export function OpenedView({
  drop,
  body,
  photo,
  firstTime,
  onClose,
  onDelete,
  mine,
}: {
  drop: Drop
  body: string
  photo: string | null
  firstTime: boolean
  mine: boolean
  onClose: () => void
  onDelete: () => void
}) {
  return (
    <div className="overlay" onClick={onClose}>
      <article className="letter" onClick={(e) => e.stopPropagation()}>
        {firstTime && <div className="stamp">ここで ひらいた</div>}
        <p className="letter__meta">{drop.authorName} より</p>
        <h2 className="letter__teaser">{drop.teaser}</h2>
        {photo && <img className="letter__photo" src={photo} alt="投稿者が撮った写真" />}
        {body && <p className="letter__body">{body}</p>}
        <p className="letter__foot">{drop.openCount}人がこの場所でひらきました</p>
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
    <div className="overlay">
      <form
        className="sheet sheet--form"
        onSubmit={(e) => {
          e.preventDefault()
          if (valid && !busy) onSubmit({ teaser, body, radius, photo })
        }}
      >
        <h2 className="sheet__title">ここに投稿を置く</h2>
        {nearMe && (
          <p className="notice">
            今いる場所にそのまま置こうとしています。自宅や職場など、知られたくない場所には置かないでください。
          </p>
        )}

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
            rows={4}
            placeholder="正門を背にして、左の塔を見上げてみて。"
            onChange={(e) => setBody(e.target.value)}
          />
          <small>{[...body].length}/600</small>
        </label>

        <div className="field">
          <span>写真（近づいた人だけ見える）</span>
          {preview ? (
            <div className="photo-pick">
              <img src={preview} alt="選んだ写真" />
              <button type="button" className="btn btn--ghost" onClick={() => pick(null)}>
                外す
              </button>
            </div>
          ) : (
            <label className="btn btn--ghost photo-pick__btn">
              写真を選ぶ
              <input type="file" accept="image/*" hidden onChange={(e) => pick(e.target.files?.[0] ?? null)} />
            </label>
          )}
        </div>

        <div className="field">
          <span>どこまで近づいたら読めるか</span>
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
                {r}m
              </button>
            ))}
          </div>
        </div>

        {error && <p className="error">{error}</p>}
        <div className="sheet__actions">
          <button type="button" className="btn btn--ghost" onClick={onCancel} disabled={busy}>
            やめる
          </button>
          <button type="submit" className="btn btn--primary" disabled={!valid || busy}>
            {busy ? '置いています…' : '置く'}
          </button>
        </div>
      </form>
    </div>
  )
}
