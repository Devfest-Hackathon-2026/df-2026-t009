import { ref, uploadBytes } from 'firebase/storage'
import { useCallback, useMemo, useState } from 'react'
import { GoogleMapView, hasMapsKey } from './components/GoogleMapView'
import { LogView } from './components/LogView'
import { RadarView } from './components/RadarView'
import { DropSheet, OpenedView, PostSheet, type PostDraft } from './components/Sheets'
import { Welcome } from './components/Welcome'
import { callCreateDrop, callDeleteDrop, callOpenDrop, storage, usingEmulators } from './firebase'
import { DEFAULT_CENTER, formatDistance, type LatLng } from './geo'
import { compressImage } from './image'
import { useLocation, type LocationMode } from './useLocation'
import { useAnonymousUser, useDrops, useMyLog } from './useData'

const NAME_KEY = 'kokodake.name'

function readName() {
  try {
    return localStorage.getItem(NAME_KEY) ?? ''
  } catch {
    return ''
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

export default function App() {
  const { user, error: authError } = useAnonymousUser()
  const uid = user?.uid
  const drops = useDrops()
  const { opened, placed } = useMyLog(uid)
  const openedIds = useMemo(() => new Set(opened.map((o) => o.id)), [opened])
  const { me, setMode, setDemoPosition } = useLocation(initialMode())

  const [name, setName] = useState(readName)
  const [view, setView] = useState<'map' | 'log'>('map')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [placing, setPlacing] = useState(false)
  const [postAt, setPostAt] = useState<LatLng | null>(null)
  const [center, setCenter] = useState<LatLng>(DEFAULT_CENTER)
  const [recenter, setRecenter] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [openedView, setOpenedView] = useState<Opened | null>(null)
  const [toast, setToast] = useState<string | null>(null)

  const selected = drops.find((d) => d.id === selectedId) ?? null
  const shownDrop = openedView ? drops.find((d) => d.id === openedView.dropId) : null

  const flash = useCallback((m: string) => {
    setToast(m)
    setTimeout(() => setToast(null), 2600)
  }, [])

  const select = useCallback((id: string | null) => {
    setSelectedId(id)
    setError(null)
  }, [])

  const openDrop = async (dropId: string) => {
    setBusy(true)
    setError(null)
    try {
      const wasOpened = openedIds.has(dropId)
      const res = await callOpenDrop({
        dropId,
        lat: me.position?.lat,
        lng: me.position?.lng,
        accuracy: me.accuracy,
      })
      const r = res.data
      if (r.status === 'too_far') {
        setError(`まだ遠いみたい（${formatDistance(r.distance)}）。半径 ${r.radius}m 以内まで近づいてね`)
        return
      }
      const mine = drops.find((d) => d.id === dropId)?.authorId === uid
      setOpenedView({ dropId, body: r.body, photo: r.photo, firstTime: !wasOpened && !mine })
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
      flash('投稿を置きました')
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const deleteDrop = async (dropId: string) => {
    if (!confirm('この投稿を消しますか？ 元に戻せません')) return
    try {
      await callDeleteDrop({ dropId })
      setOpenedView(null)
      flash('投稿を消しました')
    } catch (e) {
      flash(errorMessage(e))
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
  }

  if (!name) {
    return (
      <Welcome
        onDone={(n) => {
          try {
            localStorage.setItem(NAME_KEY, n)
          } catch {
            /* 保存できなくても続ける */
          }
          setName(n)
        }}
      />
    )
  }

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand__dot" aria-hidden />
          ここだけ
        </div>
        <button
          className={`mode ${me.mode === 'demo' ? 'mode--demo' : ''}`}
          onClick={() => setMode(me.mode === 'demo' ? 'gps' : 'demo')}
          title="デモモードでは、地図をタップした場所を現在地として扱います"
        >
          {me.mode === 'demo' ? 'デモ：タップで移動' : 'GPS'}
        </button>
      </header>

      {usingEmulators && <div className="banner">ローカルのエミュレーターに接続中</div>}
      {authError && <div className="banner banner--error">ログインに失敗しました：{authError}</div>}
      {me.error && view === 'map' && (
        <div className="banner banner--error">
          {me.error}。
          <button className="link" onClick={() => setMode('demo')}>
            デモモードで試す
          </button>
        </div>
      )}

      <main className="stage">
        {view === 'map' ? (
          <>
            {hasMapsKey ? <GoogleMapView {...mapProps} /> : <RadarView {...mapProps} />}
            {placing && (
              <div className="crosshair" aria-hidden>
                <span />
              </div>
            )}
            {!placing && !selected && (
              <button className="fab-locate" onClick={() => setRecenter((n) => n + 1)} aria-label="現在地に戻る">
                ◎
              </button>
            )}
          </>
        ) : (
          <LogView
            name={name}
            opened={opened}
            placed={placed}
            onPick={(id) => {
              setView('map')
              void openDrop(id)
            }}
          />
        )}
      </main>

      {view === 'map' && selected && !placing && (
        <DropSheet
          drop={selected}
          me={me}
          uid={uid}
          opened={openedIds.has(selected.id)}
          busy={busy}
          error={error}
          onOpen={() => openDrop(selected.id)}
          onClose={() => select(null)}
        />
      )}

      {view === 'map' && placing && (
        <div className="placebar">
          <p>地図を動かして、置きたい場所を真ん中に合わせてください</p>
          <div className="sheet__actions">
            <button className="btn btn--ghost" onClick={() => setPlacing(false)}>
              やめる
            </button>
            <button
              className="btn btn--primary"
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
          drop={shownDrop}
          body={openedView.body}
          photo={openedView.photo}
          firstTime={openedView.firstTime}
          mine={shownDrop.authorId === uid}
          onClose={() => setOpenedView(null)}
          onDelete={() => deleteDrop(shownDrop.id)}
        />
      )}

      {!placing && !selected && (
        <nav className="tabbar">
          <button className={view === 'map' ? 'on' : ''} onClick={() => setView('map')}>
            地図
          </button>
          <button
            className="tabbar__post"
            onClick={() => {
              setView('map')
              setSelectedId(null)
              setPlacing(true)
            }}
            aria-label="投稿を置く"
          >
            ＋
          </button>
          <button className={view === 'log' ? 'on' : ''} onClick={() => setView('log')}>
            ログ
          </button>
        </nav>
      )}

      {toast && <div className="toast">{toast}</div>}
    </div>
  )
}
