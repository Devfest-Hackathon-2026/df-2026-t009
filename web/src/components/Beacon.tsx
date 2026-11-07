import type { Drop } from '../types'
import { CrystalFigure, type AvatarLook } from './Crystal'
import type { PinState } from './pins'

const GLYPH: Record<PinState, string> = { mine: '★', opened: '✓', inrange: '!', locked: '?' }

/**
 * 地図に立つ投稿の印。未開封・範囲内・開封済み・自分の投稿で見た目が変わる。
 * 範囲に入ると脈打ち、選ぶと予告が吹き出しで出る。
 */
export function Beacon({ drop, state, selected, order }: { drop: Drop; state: PinState; selected: boolean; order?: number }) {
  return (
    <div className={`beacon beacon--${state} ${selected ? 'beacon--selected' : ''}`}>
      {(selected || state === 'inrange') && <span className="beacon__label">{drop.teaser}</span>}
      <span className="beacon__ring">
        <span className="beacon__face">
          <span className="beacon__glyph">{GLYPH[state]}</span>
        </span>
      </span>
      {order !== undefined && <span className="beacon__order">{order}</span>}
      <span className="beacon__tip" />
    </div>
  )
}

/** 自分の現在地。クリスタルの人形が台座ごと立つ。 */
export function Avatar({ demo, initial, look }: { demo: boolean; initial?: string; look?: AvatarLook }) {
  if (look) {
    return (
      <div className={`avatar avatar--crystal ${demo ? 'avatar--demo' : ''}`} aria-label="現在地">
        <span className="avatar__pulse" />
        <span className="avatar__shadow" />
        <CrystalFigure look={look} size={56} />
      </div>
    )
  }
  return (
    <div className={`avatar ${demo ? 'avatar--demo' : ''}`} aria-label="現在地">
      <span className="avatar__pulse" />
      <span className="avatar__core">{initial}</span>
    </div>
  )
}
