import { useCallback, useEffect, useRef, useState } from 'react'
import { callAddFootprints } from './firebase'
import { distanceMeters, type LatLng } from './geo'
import { dayKey } from './time'

/** [緯度, 経度, 時刻(ms)] */
export type TrailPoint = [number, number, number]
export type Trails = Record<string, TrailPoint[]>

const TRAIL_KEY = 'kokodake.trails'
const SHARE_KEY = 'kokodake.shareFootprints'
const MIN_STEP = 15 // これ未満の移動は記録しない（GPS のぶれ）
const MAX_DAYS = 14
const MAX_POINTS_PER_DAY = 3000

function load(): Trails {
  try {
    return JSON.parse(localStorage.getItem(TRAIL_KEY) ?? '{}') as Trails
  } catch {
    return {}
  }
}

function save(t: Trails) {
  try {
    const keys = Object.keys(t).sort().slice(-MAX_DAYS)
    localStorage.setItem(TRAIL_KEY, JSON.stringify(Object.fromEntries(keys.map((k) => [k, t[k]]))))
  } catch {
    /* 保存できなくても、その場では表示される */
  }
}

export function trailLength(points: TrailPoint[]) {
  let m = 0
  for (let i = 1; i < points.length; i++) {
    const a = { lat: points[i - 1][0], lng: points[i - 1][1] }
    const b = { lat: points[i][0], lng: points[i][1] }
    const d = distanceMeters(a, b)
    if (d < 2000) m += d // デモモードの瞬間移動は距離に数えない
  }
  return m
}

/**
 * 自分の足あと。位置が 15m 以上動くたびに、その日の道のりに足していく。
 * 端末の中だけに保存する。共有をオンにしたときだけ、マス目の単位で「みんなの踏み跡」に送る。
 */
export function useTrail(position: LatLng | null) {
  const [trails, setTrails] = useState<Trails>(load)
  const [share, setShareState] = useState(() => {
    try {
      return localStorage.getItem(SHARE_KEY) === '1'
    } catch {
      return false
    }
  })
  const pending = useRef<{ lat: number; lng: number }[]>([])

  const flush = useCallback(() => {
    const points = pending.current.splice(0, 60)
    if (points.length) callAddFootprints({ points }).catch(() => undefined)
  }, [])

  useEffect(() => {
    if (!position) return
    const key = dayKey(null)
    setTrails((prev) => {
      const today = prev[key] ?? []
      const last = today[today.length - 1]
      if (last && distanceMeters({ lat: last[0], lng: last[1] }, position) < MIN_STEP) return prev
      const next = { ...prev, [key]: [...today, [position.lat, position.lng, Date.now()] as TrailPoint].slice(-MAX_POINTS_PER_DAY) }
      save(next)
      if (share) {
        pending.current.push(position)
        if (pending.current.length >= 20) flush()
      }
      return next
    })
  }, [position?.lat, position?.lng]) // eslint-disable-line react-hooks/exhaustive-deps

  // 共有オンのあいだは、30 秒ごとにまとめて送る
  useEffect(() => {
    if (!share) return
    const t = setInterval(flush, 30_000)
    return () => {
      clearInterval(t)
      flush()
    }
  }, [share, flush])

  const setShare = useCallback((v: boolean) => {
    setShareState(v)
    try {
      localStorage.setItem(SHARE_KEY, v ? '1' : '0')
    } catch {
      /* 次回は既定（オフ）に戻る */
    }
  }, [])

  const clearTrails = useCallback(() => {
    setTrails({})
    save({})
  }, [])

  return { trails, today: trails[dayKey(null)] ?? [], share, setShare, clearTrails }
}
