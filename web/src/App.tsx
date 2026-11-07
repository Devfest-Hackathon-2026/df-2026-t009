import { ref, uploadBytes } from 'firebase/storage'
import { useCallback, useMemo, useState } from 'react'
import { FeedView } from './components/FeedView'
import { IconFeed, IconHeart, IconLocate, IconMap, IconPlus } from './components/Icons'
import { GoogleMapView, hasMapsKey } from './components/GoogleMapView'
import { NoticeView } from './components/NoticeView'
import { PIN_GLYPH, pinState } from './components/pins'
import { ProfileView } from './components/ProfileView'
import { RadarView } from './components/RadarView'
import { DropSheet, OpenedView, PostSheet, type PostDraft } from './components/Sheets'
import { Welcome } from './components/Welcome'
import {
  callAddComment,
  callCreateDrop,
  callDeleteDrop,
  callOpenDrop,
  callToggleLike,
  storage,
  usingEmulators,
} from './firebase'
import { DEFAULT_CENTER, distanceMeters, formatDistance, type LatLng } from './geo'
import { compressImage } from './image'
import { levelOf } from './types'
import { useLocation, type LocationMode } from './useLocation'
import {
  useAnonymousUser,
  useComments,
  useDrops,
  useLikedIds,
  useMyLog,
  useNotices,
  useProfile,
} from './useData'

const NAME_KEY = 'kokodake.name'
const SEEN_KEY = 'kokodake.noticesSeen'

function readStore(key: string) {
  try {
    return localStorage.getItem(key) ?? ''
  } catch {
    return ''
  }
}
function writeStore(key: string, v: string) {
  try {
    localStorage.setItem(key, v)
  } catch {
    /* 保存できなくても続ける */
  }
}

function initialMode(): LocationMode {
  const q = new URLSearchParams(location.search)
  return q.get('demo') === '1' ? 'demo' : 'gps'
}

function errorMessage(e: unknown) {
  const m = (e as { message?: string })?.message
  return m && !m.startsWith('internal') ? m : 'うまくいきませんでした。もう一度試してください'
}

type Opened = { dropId: string; body: string; photo: string | null; firstTime: boolean }
type View = 'map' | 'feed' | 'notices' | 'me'

export default function App() {
  const { user, error: authError } = useAnonymousUser()
  const uid = user?.uid
  const drops = useDrops()
  const { opened, placed } = useMyLog(uid)
  const notices = useNotices(uid)
  const likedIds = useLikedIds(uid)
  const myProfile = useProfile(uid)
  const openedIds = useMemo(() => new Set(opened.map((o) => o.id)), [opened])
  const { me, setMode, setDemoPosition } = useLocation(initialMode())

  const [name, setName] = useState(() => readStore(NAME_KEY))
  const [view, setView] = useState<View>('map')
  const [profileUid, setProfileUid] = useState<string | null>(null)
  const otherProfile = useProfile(profileUid)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [placing, setPlacing] = useState(false)
  const [postAt, setPostAt] = useState<LatLng | null>(null)
  const [center, setCenter] = useState<LatLng>(DEFAULT_CENTER)
  const [recenter, setRecenter] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [openedView, setOpenedView] = useState<Opened | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)
  const [toast, setToast] = useState<string | null>(null)
  const [lastSeen, setLastSeen] = useState(() => Number(readStore(SEEN_KEY)) || 0)

  const comments = useComments(openedView?.dropId ?? null, !!openedView)
  const selected = drops.find((d) => d.id === selectedId) ?? null
  const shownDrop = openedView ? drops.find((d) => d.id === openedView.dropId) : null
  const unread = notices.filter((n) => (n.createdAt?.toDate().getTime() ?? 0) > lastSeen).length
  const lv = levelOf(myProfile)

  // まだひらいていない中で、いちばん近いビーコン
  const nearest = useMemo(() => {
    if (!me.position) return null
    let best: { id: string; dist: number } | null = null
    for (const d of drops) {
      if (d.authorId === uid || openedIds.has(d.id)) continue
      const dist = distanceMeters(me.position, d) - d.radius
      if (!best || dist < best.dist) best = { id: d.id, dist }
    }
    return best
  }, [drops, me.position, uid, openedIds])

  const flash = useCallback((m: string) => {
    setToast(m)
    setTimeout(() => setToast(null), 2600)
  }, [])

  const select = useCallback((id: string | null) => {
    setSelectedId(id)
    setError(null)
  }, [])

  const goTo = (v: View) => {
    setView(v)
    setProfileUid(null)
    setSelectedId(null)
    if (v === 'notices') {
      const now = Date.now()
      setLastSeen(now)
      writeStore(SEEN_KEY, String(now))
    }
  }

  const pickDrop = (id: string) => {
    goTo('map')
    if (drops.some((d) => d.id === id)) setSelectedId(id)
    else flash('この投稿は消されたようです')
  }

  const showAuthor = (authorId: string) => {
    setOpenedView(null)
    setSelectedId(null)
    if (authorId === uid) return goTo('me')
    setProfileUid(authorId)
  }

  const openDrop = async (dropId: string) => {
    setBusy(true)
    setError(null)
    try {
      const res = await callOpenDrop({
        dropId,
        lat: me.position?.lat,
        lng: me.position?.lng,
        accuracy: me.accuracy,
        name,
      })
      const r = res.data
      if (r.status === 'too_far') {
        setError(`まだ遠いみたい（${formatDistance(r.distance)}）。半径 ${r.radius}m 以内まで近づいてね`)
        return
      }
      setOpenedView({ dropId, body: r.body, photo: r.photo, firstTime: r.firstTime })
      setSelectedId(null)
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const submitPost = async (d: PostDraft) => {
    if (!uid || !postAt) return
    setBusy(true)
    setError(null)
    try {
      let photoPath: string | null = null
      if (d.photo) {
        const blob = await compressImage(d.photo)
        photoPath = `uploads/${uid}/${crypto.randomUUID()}.jpg`
        await uploadBytes(ref(storage, photoPath), blob, { contentType: 'image/jpeg' })
      }
      await callCreateDrop({
        teaser: d.teaser,
        body: d.body,
        radius: d.radius,
        lat: postAt.lat,
        lng: postAt.lng,
        authorName: name,
        photoPath,
      })
      setPostAt(null)
      flash('ビーコンを置きました +15xp')
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const deleteDrop = async (dropId: string) => {
    setConfirmDelete(null)
    try {
      await callDeleteDrop({ dropId })
      setOpenedView(null)
      flash('投稿を消しました')
    } catch (e) {
      flash(errorMessage(e))
    }
  }

  const toggleLike = async (dropId: string) => {
    try {
      await callToggleLike({ dropId, name })
    } catch (e) {
      flash(errorMessage(e))
    }
  }

  const addComment = async (dropId: string, text: string) => {
    try {
      await callAddComment({ dropId, text, name })
      return true
    } catch (e) {
      flash(errorMessage(e))
      return false
    }
  }

  const mapProps = {
    drops,
    me,
    uid,
    openedIds,
    selectedId,
    onSelect: select,
    placing,
    onCenterChange: setCenter,
    onTapLocation: setDemoPosition,
    recenterSignal: recenter,
    initial: [...name][0],
  }

  if (!name) {
    return (
      <Welcome
        onDone={(n) => {
          writeStore(NAME_KEY, n)
          setName(n)
        }}
      />
    )
  }

  const onMap = view === 'map' && !profileUid
  const nearestDrop = nearest ? drops.find((d) => d.id === nearest.id) : null
  const tabOn = (v: View) => view === v && !profileUid

  return (
    <div className={`app ${onMap ? 'app--map' : ''}`}>
      <header className={`hud ${onMap ? 'hud--float' : ''}`}>
        <div className="logo">
          ここだけ<span className="logo__dot" />
        </div>
        <div className="hud__right">
          <button className="lvpill" onClick={() => goTo('me')} title="レベル">
            <span className="lvpill__lv">Lv.{lv.level}</span>
            <span className="lvpill__bar">
              <span style={{ width: `${Math.round(lv.progress * 100)}%` }} />
            </span>
          </button>
          <button
            className={`mode ${me.mode === 'demo' ? 'mode--demo' : ''}`}
            onClick={() => setMode(me.mode === 'demo' ? 'gps' : 'demo')}
            title="デモモードでは、地図をタップした場所を現在地として扱います"
          >
            {me.mode === 'demo' ? 'デモ：タップで移動' : 'GPS'}
          </button>
        </div>
      </header>

      {usingEmulators && <div className="banner">ローカルのエミュレーターに接続中</div>}
      {authError && <div className="banner banner--error">ログインに失敗しました：{authError}</div>}
      {me.error && onMap && (
        <div className="banner banner--error">
          {me.error}。
          <button className="link" onClick={() => setMode('demo')}>
            デモモードで試す
          </button>
        </div>
      )}

      <main className="stage">
        {view === 'map' && (
          <>
            {hasMapsKey ? <GoogleMapView {...mapProps} /> : <RadarView {...mapProps} />}
            {placing && (
              <div className="crosshair" aria-hidden>
                <span />
              </div>
            )}
            {!placing && !selected && (
              <button className="fab-locate" onClick={() => setRecenter((n) => n + 1)} aria-label="現在地に戻る">
                <IconLocate />
              </button>
            )}
            {!placing && !selected && nearestDrop && nearest && (
              <button className="nearest" onClick={() => setSelectedId(nearestDrop.id)}>
                <span className={`nearest__ring st-${pinState(nearestDrop, uid, openedIds, me.position, me.accuracy)}`}>
                  <span>{PIN_GLYPH[pinState(nearestDrop, uid, openedIds, me.position, me.accuracy)]}</span>
                </span>
                <span className="nearest__text">
                  {nearest.dist <= 0 ? 'ひらける投稿があります' : `次の投稿まで あと ${formatDistance(nearest.dist)}`}
                  <small>{nearestDrop.teaser}</small>
                </span>
                <span className="nearest__go">見る</span>
              </button>
            )}
          </>
        )}
        {view === 'feed' && (
          <FeedView drops={drops} me={me} uid={uid} openedIds={openedIds} onPick={pickDrop} onAuthor={showAuthor} />
        )}
        {view === 'notices' && <NoticeView notices={notices} lastSeen={lastSeen} onPick={pickDrop} />}
        {view === 'me' && (
          <ProfileView
            name={name}
            profile={myProfile}
            isMe
            opened={opened}
            placed={placed}
            theirDrops={[]}
            onPick={pickDrop}
          />
        )}
        {profileUid && (
          <div className="stage__cover">
            <ProfileView
              name={otherProfile?.name ?? drops.find((d) => d.authorId === profileUid)?.authorName ?? '名無し'}
              profile={otherProfile}
              isMe={false}
              opened={[]}
              placed={[]}
              theirDrops={drops.filter((d) => d.authorId === profileUid)}
              onPick={pickDrop}
              onBack={() => setProfileUid(null)}
            />
          </div>
        )}
      </main>

      {onMap && selected && !placing && (
        <DropSheet
          drop={selected}
          me={me}
          uid={uid}
          opened={openedIds.has(selected.id)}
          busy={busy}
          error={error}
          onOpen={() => openDrop(selected.id)}
          onClose={() => select(null)}
          onAuthor={showAuthor}
        />
      )}

      {onMap && placing && (
        <div className="placebar">
          <p>地図を動かして、ビーコンを置く場所を真ん中に合わせてください</p>
          <div className="sheet__actions">
            <button className="btn btn--ghost" onClick={() => setPlacing(false)}>
              やめる
            </button>
            <button
              className="btn btn--primary btn--glow"
              onClick={() => {
                setPostAt(hasMapsKey ? center : (me.position ?? center))
                setPlacing(false)
                setError(null)
              }}
            >
              ここに置く
            </button>
          </div>
        </div>
      )}

      {postAt && (
        <PostSheet at={postAt} me={me} busy={busy} error={error} onSubmit={submitPost} onCancel={() => setPostAt(null)} />
      )}

      {openedView && shownDrop && (
        <OpenedView
          key={openedView.dropId}
          drop={shownDrop}
          body={openedView.body}
          photo={openedView.photo}
          firstTime={openedView.firstTime}
          mine={shownDrop.authorId === uid}
          liked={likedIds.has(shownDrop.id)}
          comments={comments}
          onLike={() => toggleLike(shownDrop.id)}
          onComment={(t) => addComment(shownDrop.id, t)}
          onAuthor={showAuthor}
          onClose={() => setOpenedView(null)}
          onDelete={() => setConfirmDelete(shownDrop.id)}
        />
      )}

      {confirmDelete && (
        <div className="overlay overlay--top" onClick={() => setConfirmDelete(null)}>
          <div className="dialog" onClick={(e) => e.stopPropagation()}>
            <p>この投稿を消しますか？ 寄せ書きも一緒に消え、元に戻せません。</p>
            <div className="sheet__actions">
              <button className="btn btn--ghost" onClick={() => setConfirmDelete(null)}>
                やめる
              </button>
              <button className="btn btn--danger" onClick={() => deleteDrop(confirmDelete)}>
                消す
              </button>
            </div>
          </div>
        </div>
      )}

      {!placing && !selected && (
        <nav className="tabbar">
          <button className={tabOn('map') ? 'on' : ''} onClick={() => goTo('map')} aria-label="地図">
            <IconMap filled={tabOn('map')} />
          </button>
          <button className={tabOn('feed') ? 'on' : ''} onClick={() => goTo('feed')} aria-label="フィード">
            <IconFeed filled={tabOn('feed')} />
          </button>
          <button
            className="tabbar__post"
            onClick={() => {
              goTo('map')
              setPlacing(true)
            }}
            aria-label="投稿を置く"
          >
            <IconPlus />
          </button>
          <button className={tabOn('notices') ? 'on' : ''} onClick={() => goTo('notices')} aria-label="お知らせ">
            <span className="tabbar__icon">
              <IconHeart filled={tabOn('notices')} />
              {unread > 0 && <span className="badge">{unread > 9 ? '9+' : unread}</span>}
            </span>
          </button>
          <button className={tabOn('me') ? 'on' : ''} onClick={() => goTo('me')} aria-label="じぶん">
            <span className={`tabbar__me ${tabOn('me') ? 'tabbar__me--on' : ''}`}>{[...name][0]}</span>
          </button>
        </nav>
      )}

      {toast && <div className="toast">{toast}</div>}
    </div>
  )
}
