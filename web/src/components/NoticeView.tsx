import { timeAgo } from '../time'
import type { Notice } from '../types'

const ICON: Record<Notice['type'], string> = { open: '👣', comment: '💬', like: '♥' }

function message(n: Notice) {
  if (n.type === 'open') return <>あなたのビーコン「{n.teaser}」を、{n.actorName} さんが現地でひらきました</>
  if (n.type === 'like') return <>{n.actorName} さんが「{n.teaser}」に ♥ しました</>
  return (
    <>
      {n.actorName} さんが「{n.teaser}」に寄せ書き：「{n.text}」
    </>
  )
}

export function NoticeView({
  notices,
  lastSeen,
  onPick,
}: {
  notices: Notice[]
  lastSeen: number
  onPick: (dropId: string) => void
}) {
  return (
    <div className="page notices">
      <header className="page__head">
        <h2>お知らせ</h2>
      </header>
      {notices.length === 0 ? (
        <p className="empty">まだお知らせはありません。ビーコンを置くと、ひらかれたときにここに届きます。</p>
      ) : (
        <ul className="list">
          {notices.map((n) => {
            const unread = (n.createdAt?.toDate().getTime() ?? 0) > lastSeen
            return (
              <li key={n.id} className={unread ? 'unread' : ''}>
                <button onClick={() => onPick(n.dropId)}>
                  <span className={`notice-icon notice-icon--${n.type}`}>{ICON[n.type]}</span>
                  <span className="notice-text">
                    {message(n)}
                    <span className="muted small"> {timeAgo(n.createdAt)}</span>
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
