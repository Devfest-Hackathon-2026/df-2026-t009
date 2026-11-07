import type { Drop } from '../types'
import type { PinState } from './pins'

const GLYPH: Record<PinState, string> = { mine: '★', opened: '✓', inrange: '!', locked: '?' }

/**
 * 地図に立つ投稿。ストーリーのリングのように、未開封はグラデーション、開封済みはグレー。
 * 範囲に入ると脈打ち、選ぶと予告が吹き出しで出る。
 */
export function Beacon({ drop, state, selected }: { drop: Drop; state: PinState; selected: boolean }) {
  return (
    <div className={`beacon beacon--${state} ${selected ? 'beacon--selected' : ''}`}>
      {(selected || state === 'inrange') && <span className="beacon__label">{drop.teaser}</span>}
      <span className="beacon__ring">
        <span className="beacon__face">
          <span className="beacon__glyph">{GLYPH[state]}</span>
        </span>
      </span>
      <span className="beacon__tip" />
    </div>
  )
}

/** 自分の現在地。 */
export function Avatar({ demo, initial }: { demo: boolean; initial?: string }) {
  return (
    <div className={`avatar ${demo ? 'avatar--demo' : ''}`} aria-label="現在地">
      <span className="avatar__pulse" />
      <span className="avatar__core">{initial}</span>
    </div>
  )
}
