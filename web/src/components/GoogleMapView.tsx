import { AdvancedMarker, APIProvider, ColorScheme, Map, useMap, type MapMouseEvent } from '@vis.gl/react-google-maps'
import { useEffect, useRef, useState } from 'react'
import { DEFAULT_CENTER, type LatLng } from '../geo'
import { Avatar, Beacon } from './Beacon'
import type { MapViewProps } from './MapTypes'
import { PIN_COLOR, pinState } from './pins'

const API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY as string
const MAP_ID = (import.meta.env.VITE_GOOGLE_MAPS_MAP_ID as string) || 'DEMO_MAP_ID'

export const hasMapsKey = Boolean(API_KEY)

function Circle({ center, radius, options }: { center: LatLng; radius: number; options: google.maps.CircleOptions }) {
  const map = useMap()
  const ref = useRef<google.maps.Circle | null>(null)
  useEffect(() => {
    if (!map) return
    const c = new google.maps.Circle({ map, clickable: false })
    ref.current = c
    return () => c.setMap(null)
  }, [map])
  useEffect(() => {
    ref.current?.setOptions({ center, radius, clickable: false, ...options })
  })
  return null
}

/**
 * カメラ。追従中は自分の位置を画面の中心に保つ（位置情報ゲームのように）。
 * 地図を指で動かすと追従をやめ、「現在地に戻る」で再開する。
 */
function Camera({
  target,
  follow,
  signal,
  focus,
}: {
  target: LatLng | null
  follow: boolean
  signal: number
  focus: LatLng | null
}) {
  const map = useMap()
  // 投稿を選んだら、その場所へ寄せる（シートに隠れないよう少し下にずらす）
  useEffect(() => {
    if (!map || !focus) return
    map.panTo(focus)
    map.panBy(0, 120)
  }, [map, focus?.lat, focus?.lng]) // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (map && target && follow) map.panTo(target)
  }, [map, target?.lat, target?.lng, follow]) // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (map && target && signal > 0) map.moveCamera({ center: target, zoom: 17, tilt: 45 })
  }, [signal, map]) // eslint-disable-line react-hooks/exhaustive-deps
  return null
}

export function GoogleMapView(p: MapViewProps) {
  const myPos = p.me.position
  const [follow, setFollow] = useState(true)

  useEffect(() => {
    if (p.recenterSignal > 0) setFollow(true)
  }, [p.recenterSignal])
  useEffect(() => {
    if (p.placing) setFollow(false)
  }, [p.placing])

  const handleClick = (e: MapMouseEvent) => {
    if (p.placing) return
    const ll = e.detail.latLng
    if (p.me.mode === 'demo' && ll) p.onTapLocation(ll)
    else p.onSelect(null)
  }

  return (
    <APIProvider apiKey={API_KEY} language="ja" region="JP">
      <Map
        className="map"
        mapId={MAP_ID}
        colorScheme={ColorScheme.LIGHT}
        defaultCenter={myPos ?? DEFAULT_CENTER}
        defaultZoom={17}
        defaultTilt={45}
        defaultHeading={0}
        gestureHandling="greedy"
        disableDefaultUI
        clickableIcons={false}
        onClick={handleClick}
        onDragstart={() => setFollow(false)}
        onCameraChanged={(e) => p.onCenterChange(e.detail.center)}
      >
        <Camera
          target={myPos}
          follow={follow && !p.placing && !p.selectedId}
          signal={p.recenterSignal}
          focus={p.drops.find((d) => d.id === p.selectedId) ?? null}
        />

        {p.drops.map((d) => {
          const state = pinState(d, p.uid, p.openedIds, myPos, p.me.accuracy)
          const sel = d.id === p.selectedId
          return (
            <Circle
              key={`c-${d.id}`}
              center={d}
              radius={d.radius}
              options={{
                strokeColor: PIN_COLOR[state],
                strokeOpacity: sel || state === 'inrange' ? 0.85 : 0.3,
                strokeWeight: sel ? 2.5 : 1.2,
                fillColor: PIN_COLOR[state],
                fillOpacity: sel ? 0.08 : state === 'inrange' ? 0.05 : 0.02,
                zIndex: 1,
              }}
            />
          )
        })}

        {p.drops.map((d) => {
          const state = pinState(d, p.uid, p.openedIds, myPos, p.me.accuracy)
          const sel = d.id === p.selectedId
          return (
            <AdvancedMarker
              key={d.id}
              position={d}
              zIndex={sel ? 1000 : state === 'inrange' ? 500 : 10}
              onClick={() => !p.placing && p.onSelect(d.id)}
            >
              <Beacon drop={d} state={state} selected={sel} />
            </AdvancedMarker>
          )
        })}

        {myPos && (
          <>
            {p.me.mode === 'gps' && p.me.accuracy && p.me.accuracy > 15 ? (
              <Circle
                center={myPos}
                radius={p.me.accuracy}
                options={{ strokeOpacity: 0, fillColor: '#0095f6', fillOpacity: 0.12, zIndex: 2 }}
              />
            ) : null}
            <AdvancedMarker position={myPos} zIndex={2000}>
              <Avatar demo={p.me.mode === "demo"} initial={p.initial} />
            </AdvancedMarker>
          </>
        )}
      </Map>
    </APIProvider>
  )
}
