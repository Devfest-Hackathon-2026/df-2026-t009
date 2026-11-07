/** 2点間の距離（メートル）。ハバーサイン公式。 */
export function distanceMeters(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const R = 6371008.8;
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(lat2 - lat1);
  const dLng = toRad(lng2 - lng1);
  const a =
    Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.min(1, Math.sqrt(a)));
}

/** GPS の誤差として許容する最大値（メートル）。 */
export const MAX_ACCURACY_ALLOWANCE = 30;

export const ALLOWED_RADII = [100, 300, 500] as const;

/**
 * 範囲内かどうか。端末が報告する精度（accuracy）を最大 30m まで差し引いて判定する。
 * 精度が悪い端末ほど有利にならないよう上限を設けている。
 */
export function isWithinRadius(distance: number, radius: number, accuracy: number | undefined): boolean {
  const allowance = Math.min(Math.max(accuracy ?? 0, 0), MAX_ACCURACY_ALLOWANCE);
  return distance - allowance <= radius;
}

export function isValidLatLng(lat: unknown, lng: unknown): lat is number {
  return (
    typeof lat === "number" &&
    typeof lng === "number" &&
    Number.isFinite(lat) &&
    Number.isFinite(lng) &&
    lat >= -90 &&
    lat <= 90 &&
    lng >= -180 &&
    lng <= 180
  );
}
