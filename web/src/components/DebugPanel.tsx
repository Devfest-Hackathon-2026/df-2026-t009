import { useEffect, useRef, useState } from 'react'
import { distanceMeters, formatDistance, moveByMeters, type LatLng } from '../geo'
import type { Drop } from '../types'
import type { Me } from '../useLocation'

export type DebugView = { tilt: boolean; paper: boolean }

const PLACES: { name: string; at: LatLng }[] = [
  { name: '早稲田', at: { lat: 35.7088, lng: 139.7222 } },
  { name: '渋谷', at: { lat: 35.6595, lng: 139.7005 } },
  { name: '浅草', at: { lat: 35.7148, lng: 139.7967 } },
  { name: '京都・祇園', at: { lat: 35.0037, lng: 135.7788 } },
  { name: '大阪・難波', at: { lat: 34.6661, lng: 135.5011 } },
]

const WALK_SPEED = 1.4 * 8 // m/秒（実際の8倍速）
const TICK = 500

/**
 * 開発・審査用のデバッグパネル。?debug=1 か、ロゴを5回タップで開く。
 * 位置の操作はデモモードで行う。距離の判定はサーバーなので、ここから遠くの投稿を開くことはできない。
 */
export function DebugPanel({
  me,
  uid,
  drops,
  selectedId,
  view,
  onView,
  theme,
  onTheme,
  onMove,
  onDemo,
  onSeed,
  onDeleteMine,
  onPlayUnlock,
  onPlayComplete,
  onReset,
  onClose,
}: {
  me: Me
  uid: string | undefined
  drops: Drop[]
  selectedId: string | null
  view: DebugView
  onView: (v: DebugView) => void
  theme: 'holo' | 'milk'
  onTheme: (t: 'holo' | 'milk') => void
  onMove: (p: LatLng) => void
  onDemo: () => void
  onSeed: () => Promise<string>
  onDeleteMine: () => Promise<string>
  onPlayUnlock: () => void
  onPlayComplete: () => void
  onReset: () => void
  onClose: () => void
}) {
  const [msg, setMsg] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [walkTo, setWalkTo] = useState<string | null>(null)
  const posRef = useRef(me.position)
  posRef.current = me.position

  const target = drops.find((d) => d.id === (walkTo ?? selectedId)) ?? null
  const pos = me.position
  const dist = pos && target ? distanceMeters(pos, target) : null

  // 選んだ投稿まで、まっすぐ歩いて近づく
  useEffect(() => {
    if (!walkTo) return
    const t = setInterval(() => {
      const p = posRef.current
      const goal = drops.find((d) => d.id === walkTo)
      if (!p || !goal) return setWalkTo(null)
      const d = distanceMeters(p, goal)
      const stopAt = Math.max(10, goal.radius * 0.5)
      if (d <= stopAt) {
        setWalkTo(null)
        setMsg('到着しました。下の「ひらく」を押せます')
        return
      }
      const step = Math.min(WALK_SPEED * (TICK / 1000), d - stopAt)
      const k = step / d
      onMove({ lat: p.lat + (goal.lat - p.lat) * k, lng: p.lng + (goal.lng - p.lng) * k })
    }, TICK)
    return () => clearInterval(t)
  }, [walkTo, drops, onMove])

  const nudge = (x: number, y: number) => pos && onMove(moveByMeters(pos, x, y))
  const run = async (f: () => Promise<string>) => {
    setBusy(true)
    try {
      setMsg(await f())
    } catch (e) {
      setMsg(String((e as Error)?.message ?? e))
    } finally {
      setBusy(false)
    }
  }

  const build = (import.meta.env.VITE_BUILD_TIME as string | undefined) ?? 'dev'

  return (
    <aside className="debug" role="dialog" aria-label="デバッグ">
      <header className="debug__head">
        <b>DEBUG</b>
        <button onClick={onClose} aria-label="とじる">
          ×
        </button>
      </header>

      {me.mode !== 'demo' && (
        <button className="debug__warn" onClick={onDemo}>
          いまは GPS モード。位置を動かすにはデモモードに切り替え →
        </button>
      )}

      <section>
        <h4>ワープ</h4>
        <div className="debug__chips">
          {PLACES.map((p) => (
            <button key={p.name} disabled={me.mode !== 'demo'} onClick={() => onMove(p.at)}>
              {p.name}
            </button>
          ))}
        </div>
      </section>

      <section>
        <h4>すこし動く</h4>
        <div className="debug__pad">
          {[10, 50].map((m) => (
            <div key={m} className="debug__padrow">
              <span>{m}m</span>
              <button disabled={me.mode !== 'demo'} onClick={() => nudge(0, m)}>↑</button>
              <button disabled={me.mode !== 'demo'} onClick={() => nudge(0, -m)}>↓</button>
              <button disabled={me.mode !== 'demo'} onClick={() => nudge(-m, 0)}>←</button>
              <button disabled={me.mode !== 'demo'} onClick={() => nudge(m, 0)}>→</button>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h4>歩いて近づく</h4>
        {target ? (
          <p className="debug__small">
            「{target.teaser}」まで {dist !== null ? formatDistance(dist) : '—'}（半径 {formatDistance(target.radius)}）
          </p>
        ) : (
          <p className="debug__small">地図で投稿をひとつ選ぶと、そこまで歩けます</p>
        )}
        <div className="debug__chips">
          <button disabled={!target || me.mode !== 'demo' || !!walkTo} onClick={() => target && setWalkTo(target.id)}>
            ▶ 歩く（8倍速）
          </button>
          <button disabled={!walkTo} onClick={() => setWalkTo(null)}>
            ■ 止める
          </button>
        </div>
      </section>

      <section>
        <h4>テストデータ</h4>
        <div className="debug__chips">
          <button disabled={busy || !pos} onClick={() => run(onSeed)}>
            ＋ まわりに3件置く
          </button>
          <button disabled={busy} onClick={() => run(onDeleteMine)}>
            自分の【テスト】を消す
          </button>
        </div>
      </section>

      <section>
        <h4>演出</h4>
        <div className="debug__chips">
          <button onClick={onPlayUnlock}>ひらいた！</button>
          <button onClick={onPlayComplete}>COMPLETE!</button>
        </div>
      </section>

      <section>
        <h4>表示</h4>
        <div className="debug__chips">
          <button className={theme === 'holo' ? 'on' : ''} onClick={() => onTheme(theme === 'holo' ? 'milk' : 'holo')}>
            テーマ {theme === 'holo' ? 'HOLO' : 'MILK'}
          </button>
          <button className={view.tilt ? 'on' : ''} onClick={() => onView({ ...view, tilt: !view.tilt })}>
            立体 {view.tilt ? 'ON' : 'OFF'}
          </button>
          <button className={view.paper ? 'on' : ''} onClick={() => onView({ ...view, paper: !view.paper })}>
            紙の質感 {view.paper ? 'ON' : 'OFF'}
          </button>
        </div>
      </section>

      <section>
        <h4>状態</h4>
        <dl className="debug__info">
          <dt>uid</dt>
          <dd>{uid ?? '—'}</dd>
          <dt>モード</dt>
          <dd>{me.mode}</dd>
          <dt>現在地</dt>
          <dd>{pos ? `${pos.lat.toFixed(5)}, ${pos.lng.toFixed(5)}` : '—'}</dd>
          <dt>精度</dt>
          <dd>{me.accuracy !== undefined ? `${Math.round(me.accuracy)}m` : '—'}</dd>
          <dt>投稿数</dt>
          <dd>{drops.length}</dd>
          <dt>Firebase</dt>
          <dd>{import.meta.env.VITE_FIREBASE_PROJECT_ID}</dd>
          <dt>ビルド</dt>
          <dd>{build}</dd>
        </dl>
      </section>

      <section>
        <button className="debug__danger" onClick={onReset}>
          この端末の記録をリセットして最初から
        </button>
      </section>

      {msg && <p className="debug__msg">{msg}</p>}
    </aside>
  )
}
