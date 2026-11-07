export type LatLng = { lat: number; lng: number }

export const RADII = [100, 300, 500, 1000] as const
export const ACCURACY_ALLOWANCE = 30

/** 早稲田大学 大隈講堂のあたり。GPS が使えないときとデモの初期位置。 */
export const DEFAULT_CENTER: LatLng = { lat: 35.7088, lng: 139.7222 }

export function distanceMeters(a: LatLng, b: LatLng): number {
  const R = 6371008.8
  const toRad = (d: number) => (d * Math.PI) / 180
  const dLat = toRad(b.lat - a.lat)
  const dLng = toRad(b.lng - a.lng)
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a.lat)) * Math.cos(toRad(b.lat)) * Math.sin(dLng / 2) ** 2
  return 2 * R * Math.asin(Math.min(1, Math.sqrt(h)))
}

export function isWithin(distance: number, radius: number, accuracy?: number) {
  return distance - Math.min(Math.max(accuracy ?? 0, 0), ACCURACY_ALLOWANCE) <= radius
}

/** 現在地から見た東西・南北のずれ（メートル）。 */
export function offsetMeters(from: LatLng, to: LatLng) {
  const mPerDegLat = 111_320
  const mPerDegLng = 111_320 * Math.cos((from.lat * Math.PI) / 180)
  return { x: (to.lng - from.lng) * mPerDegLng, y: (to.lat - from.lat) * mPerDegLat }
}

export function moveByMeters(from: LatLng, x: number, y: number): LatLng {
  const mPerDegLat = 111_320
  const mPerDegLng = 111_320 * Math.cos((from.lat * Math.PI) / 180)
  return { lat: from.lat + y / mPerDegLat, lng: from.lng + x / mPerDegLng }
}

export function formatDistance(m: number) {
  if (m < 1000) return `${Math.round(m)}m`
  return `${(m / 1000).toFixed(1)}km`
}
