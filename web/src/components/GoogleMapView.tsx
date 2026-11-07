import { AdvancedMarker, APIProvider, Map, useMap, type MapMouseEvent } from '@vis.gl/react-google-maps'
import { useEffect, useRef } from 'react'
import { DEFAULT_CENTER, type LatLng } from '../geo'
import type { MapViewProps } from './MapTypes'
import { PIN_GLYPH, pinState } from './pins'

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

function Recenter({ target, signal }: { target: LatLng | null; signal: number }) {
  const map = useMap()
  const didInitial = useRef(false)
  useEffect(() => {
    if (!map || !target) return
    if (!didInitial.current) {
      didInitial.current = true
      map.setCenter(target)
    }
  }, [map, target]) // 初回の現在地取得時だけ自動で寄せる
  useEffect(() => {
    if (map && target && signal > 0) map.panTo(target)
  }, [signal, map]) // eslint-disable-line react-hooks/exhaustive-deps -- signal が変わったときだけ動かす
  return null
}

export function GoogleMapView(p: MapViewProps) {
  const myPos = p.me.position

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
        defaultCenter={myPos ?? DEFAULT_CENTER}
        defaultZoom={16}
        gestureHandling="greedy"
        disableDefaultUI
        clickableIcons={false}
        onClick={handleClick}
        onCameraChanged={(e) => p.onCenterChange(e.detail.center)}
      >
        <Recenter target={myPos} signal={p.recenterSignal} />

        {p.drops.map((d) => {
          const state = pinState(d, p.uid, p.openedIds, myPos, p.me.accuracy)
          return (
            <Circle
              key={`c-${d.id}`}
              center={d}
              radius={d.radius}
              options={{
                strokeColor: state === 'inrange' ? '#e8613c' : '#8a7a63',
                strokeOpacity: d.id === p.selectedId ? 0.9 : 0.25,
                strokeWeight: d.id === p.selectedId ? 2 : 1,
                fillColor: state === 'inrange' ? '#e8613c' : '#8a7a63',
                fillOpacity: d.id === p.selectedId ? 0.12 : 0.04,
                zIndex: 1,
              }}
            />
          )
        })}

        {p.drops.map((d) => {
          const state = pinState(d, p.uid, p.openedIds, myPos, p.me.accuracy)
          return (
            <AdvancedMarker
              key={d.id}
              position={d}
              zIndex={d.id === p.selectedId ? 1000 : state === 'inrange' ? 500 : 10}
              onClick={() => !p.placing && p.onSelect(d.id)}
            >
              <div className={`pin pin--${state} ${d.id === p.selectedId ? 'pin--selected' : ''}`}>
                <span className="pin__glyph">{PIN_GLYPH[state]}</span>
                <span className="pin__label">{d.teaser}</span>
              </div>
            </AdvancedMarker>
          )
        })}

        {myPos && (
          <>
            {p.me.mode === 'gps' && p.me.accuracy && p.me.accuracy > 15 ? (
              <Circle
                center={myPos}
                radius={p.me.accuracy}
                options={{ strokeOpacity: 0, fillColor: '#2f6fde', fillOpacity: 0.1, zIndex: 2 }}
              />
            ) : null}
            <AdvancedMarker position={myPos} zIndex={2000}>
              <div className={`me ${p.me.mode === 'demo' ? 'me--demo' : ''}`} aria-label="現在地" />
            </AdvancedMarker>
          </>
        )}

      </Map>
    </APIProvider>
  )
}
