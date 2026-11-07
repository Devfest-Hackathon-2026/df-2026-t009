import { distanceMeters, isWithin, type LatLng } from '../geo'
import type { Drop } from '../types'

export type PinState = 'mine' | 'opened' | 'inrange' | 'locked'

export function pinState(
  drop: Drop,
  uid: string | undefined,
  openedIds: Set<string>,
  me: LatLng | null,
  accuracy?: number,
): PinState {
  if (drop.authorId === uid) return 'mine'
  if (openedIds.has(drop.id)) return 'opened'
  if (me && isWithin(distanceMeters(me, drop), drop.radius, accuracy)) return 'inrange'
  return 'locked'
}

export const PIN_GLYPH: Record<PinState, string> = {
  mine: '★',
  opened: '✓',
  inrange: '!',
  locked: '?',
}

/** 範囲の円の色。CSS の --st-* と合わせる。 */
export const PIN_COLOR: Record<PinState, string> = {
  mine: '#262626',
  opened: '#a8a8a8',
  inrange: '#ff2f6d',
  locked: '#8a3ab9',
}
