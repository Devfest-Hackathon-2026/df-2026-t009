/** 線のアイコン（24px グリッド）。塗りつぶし版は選択中に使う。 */
type P = { filled?: boolean }
const base = { width: 26, height: 26, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 1.9, strokeLinecap: 'round', strokeLinejoin: 'round' } as const

export const IconMap = ({ filled }: P) => (
  <svg {...base}>
    <path d="M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0C18.5 15.4 12 21 12 21Z" fill={filled ? 'currentColor' : 'none'} />
    <circle cx="12" cy="10" r="2.4" fill={filled ? '#fff' : 'none'} stroke={filled ? '#fff' : 'currentColor'} />
  </svg>
)

export const IconFeed = ({ filled }: P) => (
  <svg {...base}>
    <rect x="5" y="6" width="14" height="15" rx="3" fill={filled ? 'currentColor' : 'none'} />
    <path d="M7.5 3h9" />
  </svg>
)

export const IconPlus = () => (
  <svg {...base} strokeWidth={2.2}>
    <path d="M12 6v12M6 12h12" />
  </svg>
)

export const IconHeart = ({ filled }: P) => (
  <svg {...base}>
    <path
      d="M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20Z"
      fill={filled ? 'currentColor' : 'none'}
    />
  </svg>
)

export const IconLocate = () => (
  <svg {...base}>
    <path d="M20 4 4 11l7 2 2 7 7-16Z" />
  </svg>
)
