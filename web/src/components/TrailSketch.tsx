import { formatDistance } from '../geo'
import { trailLength, type TrailPoint } from '../useTrail'

/** その日の道のりを、小さな線画にする（地図なしで形だけ）。 */
export function TrailSketch({ points }: { points: TrailPoint[] }) {
  const lats = points.map((p) => p[0])
  const lngs = points.map((p) => p[1])
  const minLat = Math.min(...lats)
  const maxLat = Math.max(...lats)
  const minLng = Math.min(...lngs)
  const k = Math.cos((((minLat + maxLat) / 2) * Math.PI) / 180)
  const w = Math.max((Math.max(...lngs) - minLng) * k, maxLat - minLat, 1e-6)
  const xy = points.map((p) => [8 + (((p[1] - minLng) * k) / w) * 100, 8 + ((maxLat - p[0]) / w) * 100])
  const d = xy.map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`).join(' ')
  const [sx, sy] = xy[0]
  const [ex, ey] = xy[xy.length - 1]
  return (
    <div className="tile tile--trail" aria-label="この日の道のり">
      <svg viewBox="0 0 116 116">
        <path d={d} className="sketch__line" />
        <circle cx={sx} cy={sy} r="4" className="sketch__start" />
        <circle cx={ex} cy={ey} r="5" className="sketch__end" />
      </svg>
      <span className="tile__kind">足あと</span>
      <span className="tile__teaser">{formatDistance(trailLength(points))}</span>
    </div>
  )
}
