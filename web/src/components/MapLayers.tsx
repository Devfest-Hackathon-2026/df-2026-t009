import { useMap } from '@vis.gl/react-google-maps'
import { useEffect } from 'react'
import { distanceMeters } from '../geo'
import type { EventStop, Footprint } from '../types'
import type { TrailPoint } from '../useTrail'

/** 自分の足あと。白いふちの上に、点線で道のりを描く。瞬間移動（デモ）のところは切る。 */
export function TrailLayer({ points }: { points: TrailPoint[] }) {
  const map = useMap()
  useEffect(() => {
    if (!map || points.length < 2) return
    const segments: google.maps.LatLngLiteral[][] = [[]]
    points.forEach((p, i) => {
      const ll = { lat: p[0], lng: p[1] }
      if (i > 0) {
        const prev = { lat: points[i - 1][0], lng: points[i - 1][1] }
        if (distanceMeters(prev, ll) > 2000) segments.push([])
      }
      segments[segments.length - 1].push(ll)
    })
    const lines: google.maps.Polyline[] = []
    for (const path of segments.filter((s) => s.length > 1)) {
      lines.push(
        new google.maps.Polyline({ map, path, clickable: false, strokeColor: '#fffdf9', strokeOpacity: 0.9, strokeWeight: 9, zIndex: 3 }),
        new google.maps.Polyline({
          map,
          path,
          clickable: false,
          strokeOpacity: 0,
          zIndex: 4,
          icons: [
            {
              icon: { path: google.maps.SymbolPath.CIRCLE, scale: 2.6, fillColor: '#c9918b', fillOpacity: 1, strokeOpacity: 0 },
              offset: '0',
              repeat: '9px',
            },
          ],
        }),
      )
    }
    return () => lines.forEach((l) => l.setMap(null))
  }, [map, points])
  return null
}

/** みんなの踏み跡。よく通られたマス目ほど濃く、道のように浮かび上がる。 */
export function FootprintLayer({ cells }: { cells: Footprint[] }) {
  const map = useMap()
  useEffect(() => {
    if (!map || cells.length === 0) return
    const max = Math.max(...cells.map((c) => c.count))
    const circles = cells.map(
      (c) =>
        new google.maps.Circle({
          map,
          center: c,
          radius: 13,
          clickable: false,
          strokeOpacity: 0,
          fillColor: '#a7b8a2',
          fillOpacity: 0.12 + 0.38 * Math.min(1, Math.log(c.count + 1) / Math.log(max + 1)),
          zIndex: 2,
        }),
    )
    return () => circles.forEach((c) => c.setMap(null))
  }, [map, cells])
  return null
}

/** イベントの巡る順を、矢印つきの破線でつなぐ。 */
export function RouteLayer({ stops }: { stops: EventStop[] }) {
  const map = useMap()
  useEffect(() => {
    if (!map || stops.length < 2) return
    const path = stops.map((s) => ({ lat: s.lat, lng: s.lng }))
    const under = new google.maps.Polyline({ map, path, clickable: false, strokeColor: '#ffffff', strokeOpacity: 0.95, strokeWeight: 8, zIndex: 5 })
    const line = new google.maps.Polyline({
      map,
      path,
      clickable: false,
      strokeOpacity: 0,
      zIndex: 6,
      icons: [
        { icon: { path: 'M 0,-1 0,1', strokeOpacity: 1, strokeColor: '#8f7b70', strokeWeight: 4, scale: 3 }, offset: '0', repeat: '14px' },
        {
          icon: { path: google.maps.SymbolPath.FORWARD_CLOSED_ARROW, scale: 3.5, fillColor: '#8f7b70', fillOpacity: 1, strokeColor: '#fff', strokeWeight: 1.5 },
          offset: '50%',
          repeat: '120px',
        },
      ],
    })
    // ルート全体が見えるように寄せる
    const b = new google.maps.LatLngBounds()
    path.forEach((p) => b.extend(p))
    map.fitBounds(b, { top: 120, bottom: 220, left: 50, right: 50 })
    return () => {
      under.setMap(null)
      line.setMap(null)
    }
  }, [map, stops])
  return null
}
