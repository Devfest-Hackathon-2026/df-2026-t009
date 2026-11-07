import { useEffect, useState } from 'react'

/** 文字で描いたドット絵を、マス目の div で表示する。 */
function PixelArt({ art, palette, size }: { art: string[]; palette: Record<string, string>; size: number }) {
  const w = Math.max(...art.map((r) => r.length))
  return (
    <div
      className="pixelart"
      style={{ gridTemplateColumns: `repeat(${w}, ${size}px)`, gridAutoRows: `${size}px` }}
      aria-hidden
    >
      {art.flatMap((row, y) =>
        [...row.padEnd(w, '.')].map((ch, x) => (
          <span key={`${x}-${y}`} style={{ background: palette[ch] ?? 'transparent' }} />
        )),
      )}
    </div>
  )
}

const PALETTE = {
  K: '#2b1a12', // ふち
  B: '#9c5a2b', // 木
  b: '#c47a3c', // 木の明るいところ
  G: '#f5c542', // 金具
  g: '#fff2a8', // 金具のひかり
  L: '#fff6c8', // 中の光
  Y: '#ffd84a',
}

const CHEST_CLOSED = [
  '..KKKKKKKKKKKK..',
  '.KbbbbbbbbbbbbK.',
  'KbBBBBBBBBBBBBbK',
  'KbBBBBBBBBBBBBbK',
  'KKKKKKKGGKKKKKKK',
  'KGGGGGGgGGGGGGGK',
  'KKKKKKKGGKKKKKKK',
  'KbBBBBBBBBBBBBbK',
  'KbBBBBBGGBBBBBbK',
  'KbBBBBBGGBBBBBbK',
  'KbBBBBBBBBBBBBbK',
  'KbbbbbbbbbbbbbbK',
  'KKKKKKKKKKKKKKKK',
]

const CHEST_OPEN = [
  '..KKKKKKKKKKKK..',
  '.KbbbbbbbbbbbbK.',
  'KbBBBBBBBBBBBBbK',
  'KKKKKKKKKKKKKKKK',
  '.KLLLLYLLLLYLLK.',
  'KLLYLLLLLYLLLLLK',
  'KKKKKKKGGKKKKKKK',
  'KbBBBBBBBBBBBBbK',
  'KbBBBBBGGBBBBBbK',
  'KbBBBBBGGBBBBBbK',
  'KbBBBBBBBBBBBBbK',
  'KbbbbbbbbbbbbbbK',
  'KKKKKKKKKKKKKKKK',
]

const STAR = ['..Y..', '.YgY.', 'YgggY', '.YgY.', '..Y..']

/** はじめてひらいたときの、ドット絵の宝箱の演出。 */
export function PixelUnlock({ onDone }: { onDone: () => void }) {
  const [open, setOpen] = useState(false)
  useEffect(() => {
    const a = setTimeout(() => setOpen(true), 450)
    const b = setTimeout(onDone, 1900)
    return () => {
      clearTimeout(a)
      clearTimeout(b)
    }
  }, [onDone])
  return (
    <div className="pixel-scene" onClick={onDone}>
      <div className={`pixel-chest ${open ? 'pixel-chest--open' : ''}`}>
        {open && <div className="pixel-beam" />}
        <PixelArt art={open ? CHEST_OPEN : CHEST_CLOSED} palette={PALETTE} size={8} />
        {open &&
          [0, 1, 2, 3, 4, 5].map((i) => (
            <div key={i} className={`pixel-star pixel-star--${i}`}>
              <PixelArt art={STAR} palette={PALETTE} size={4} />
            </div>
          ))}
      </div>
      <div className={`pixel-text ${open ? 'pixel-text--show' : ''}`}>
        ひらいた！
        <small>+30 EXP</small>
      </div>
      <div className="pixel-window">
        {open ? 'この場所に来た人だけが読める投稿を見つけた。' : '……なにかある。'}
      </div>
    </div>
  )
}
