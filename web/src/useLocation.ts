import { useCallback, useEffect, useState } from 'react'
import { DEFAULT_CENTER, type LatLng } from './geo'

export type LocationMode = 'gps' | 'demo'

export type Me = {
  mode: LocationMode
  position: LatLng | null
  accuracy?: number
  error: string | null
}

const DEMO_KEY = 'kokodake.demoPosition'

function loadDemoPosition(): LatLng {
  try {
    const raw = localStorage.getItem(DEMO_KEY)
    if (raw) {
      const p = JSON.parse(raw)
      if (typeof p?.lat === 'number' && typeof p?.lng === 'number') return p
    }
  } catch {
    /* 読めなければ初期位置 */
  }
  return DEFAULT_CENTER
}

/**
 * 現在地。GPS モードでは端末の位置を追いかけ、デモモードでは地図をタップした場所を現在地として扱う。
 * デモモードは審査やオンライン発表で、現地に行けない人が体験するためのもの。
 */
export function useLocation(initialMode: LocationMode) {
  const [mode, setMode] = useState<LocationMode>(initialMode)
  const [gps, setGps] = useState<{ position: LatLng | null; accuracy?: number; error: string | null }>({
    position: null,
    error: null,
  })
  const [demoPosition, setDemoPositionState] = useState<LatLng>(loadDemoPosition)

  useEffect(() => {
    if (mode !== 'gps') return
    if (!('geolocation' in navigator)) {
      setGps({ position: null, error: 'この端末では位置情報が使えません' })
      return
    }
    const id = navigator.geolocation.watchPosition(
      (p) =>
        setGps({
          position: { lat: p.coords.latitude, lng: p.coords.longitude },
          accuracy: p.coords.accuracy,
          error: null,
        }),
      (e) =>
        setGps((prev) => ({
          ...prev,
          error:
            e.code === e.PERMISSION_DENIED
              ? '位置情報の利用が許可されていません'
              : '現在地を取得できませんでした',
        })),
      { enableHighAccuracy: true, maximumAge: 5_000, timeout: 20_000 },
    )
    return () => navigator.geolocation.clearWatch(id)
  }, [mode])

  const setDemoPosition = useCallback((p: LatLng) => {
    setDemoPositionState(p)
    try {
      localStorage.setItem(DEMO_KEY, JSON.stringify(p))
    } catch {
      /* 保存できなくても動く */
    }
  }, [])

  const me: Me =
    mode === 'demo'
      ? { mode, position: demoPosition, accuracy: 0, error: null }
      : { mode, position: gps.position, accuracy: gps.accuracy, error: gps.error }

  return { me, setMode, setDemoPosition }
}
