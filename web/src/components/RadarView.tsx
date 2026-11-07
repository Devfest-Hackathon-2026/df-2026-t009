import { useEffect } from 'react'
import { DEFAULT_CENTER, distanceMeters, moveByMeters, offsetMeters } from '../geo'
import { Avatar, Beacon } from './Beacon'
import type { MapViewProps } from './MapTypes'
import { pinState } from './pins'

const VIEW_RADIUS = 650

/**
 * Google Maps の API キーがないときの代わりの画面。現在地を中心に、周りの投稿を距離と方角で並べる。
 */
export function RadarView(p: MapViewProps) {
  const center = p.me.position ?? DEFAULT_CENTER

  useEffect(() => {
    p.onCenterChange(center)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [center.lat, center.lng])

  const toPct = (m: number) => 50 + (m / VIEW_RADIUS) * 50

  const handleClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (p.placing) return
    if (p.me.mode !== 'demo') return p.onSelect(null)
    const rect = e.currentTarget.getBoundingClientRect()
    const size = Math.min(rect.width, rect.height)
    const x = ((e.clientX - (rect.left + rect.width / 2)) / (size / 2)) * VIEW_RADIUS
    const y = -((e.clientY - (rect.top + rect.height / 2)) / (size / 2)) * VIEW_RADIUS
    p.onTapLocation(moveByMeters(center, x, y))
  }

  return (
    <div className="radar" onClick={handleClick}>
      <div className="radar__field">
        {[100, 300, 500].map((r) => (
          <div key={r} className="radar__ring" style={{ width: `${(r / VIEW_RADIUS) * 100}%`, height: `${(r / VIEW_RADIUS) * 100}%` }}>
            <span>{r}m</span>
          </div>
        ))}
        {p.drops.map((d) => {
          const { x, y } = offsetMeters(center, d)
          const dist = distanceMeters(center, d)
          const k = dist > VIEW_RADIUS * 0.95 ? (VIEW_RADIUS * 0.95) / dist : 1
          const state = pinState(d, p.uid, p.openedIds, p.me.position, p.me.accuracy)
          return (
            <button
              key={d.id}
              className={`radar__pin ${k < 1 ? 'radar__pin--far' : ''}`}
              style={{ left: `${toPct(x * k)}%`, top: `${100 - toPct(y * k)}%` }}
              onClick={(e) => {
                e.stopPropagation()
                if (!p.placing) p.onSelect(d.id)
              }}
            >
              <Beacon drop={d} state={state} selected={d.id === p.selectedId} />
            </button>
          )
        })}
        <div className="radar__me">
          <Avatar demo={p.me.mode === "demo"} initial={p.initial} look={p.look} />
        </div>
      </div>
      <p className="radar__note">地図の API キーが未設定のため、簡易レーダー表示です</p>
    </div>
  )
}
