import { useEffect, useRef, useState } from 'react'

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
  K: '#6b5a52', // ふち
  B: '#c9a68a', // 木
  b: '#e0c3a8', // 木の明るいところ
  G: '#d6a39c', // 金具
  g: '#fbe9e4', // 金具のひかり
  L: '#fff8ee', // 中の光
  Y: '#f0c9c0',
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

/** ドット絵の宝箱の演出。はじめてひらいたときと、イベントを制覇したときに使う。 */
export function PixelUnlock({
  onDone,
  title = "ひらいた！",
  sub = "+30 EXP",
  message = "この場所に来た人だけが読める投稿を見つけた。",
  before = "……なにかある。",
  long = false,
}: {
  onDone: () => void
  title?: string
  sub?: string
  message?: string
  before?: string
  long?: boolean
}) {
  const [open, setOpen] = useState(false)
  const done = useRef(onDone)
  done.current = onDone
  useEffect(() => {
    const a = setTimeout(() => setOpen(true), 450)
    const b = setTimeout(() => done.current(), long ? 3200 : 1900)
    return () => {
      clearTimeout(a)
      clearTimeout(b)
    }
  }, [long])
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
        {title}
        <small>{sub}</small>
      </div>
      <div className="pixel-window">
        {open ? message : before}
      </div>
    </div>
  )
}
