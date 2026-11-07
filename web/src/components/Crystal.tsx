import { useId } from 'react'

export type Pose = 'stand' | 'walk' | 'wave' | 'point'
export type Tint = 'cyan' | 'amber' | 'rose' | 'clear'
export type AvatarLook = { pose: Pose; tint: Tint }

export const POSES: { id: Pose; label: string }[] = [
  { id: 'stand', label: '立つ' },
  { id: 'walk', label: '歩く' },
  { id: 'wave', label: '手をふる' },
  { id: 'point', label: '指さす' },
]

export const TINTS: { id: Tint; label: string; light: string; mid: string; dark: string; glow: string }[] = [
  { id: 'cyan', label: 'シアン', light: '#e8fcff', mid: '#7fe3ff', dark: '#1f7f99', glow: '#7fe3ff' },
  { id: 'amber', label: 'アンバー', light: '#fff4df', mid: '#ffbe5c', dark: '#a8651a', glow: '#ffb24a' },
  { id: 'rose', label: 'ローズ', light: '#ffeef3', mid: '#ff9fbf', dark: '#a3456a', glow: '#ff9fbf' },
  { id: 'clear', label: 'クリア', light: '#ffffff', mid: '#cfe2ea', dark: '#6f8c99', glow: '#e6f4f7' },
]

const AVATAR_KEY = 'kokodake.avatar'

export function loadLook(): AvatarLook {
  try {
    const v = JSON.parse(localStorage.getItem(AVATAR_KEY) ?? 'null')
    if (POSES.some((p) => p.id === v?.pose) && TINTS.some((t) => t.id === v?.tint)) return v
  } catch {
    /* 読めなければ初期値 */
  }
  return { pose: 'stand', tint: 'cyan' }
}

export function saveLook(l: AvatarLook) {
  try {
    localStorage.setItem(AVATAR_KEY, JSON.stringify(l))
  } catch {
    /* 保存できなくても、その場では使える */
  }
}

// 40×64 のマス目に描いた、台座つきの小さな人形（武器は持たない）
const HEAD = '20,3 24.5,6 24.5,12 20,15 15.5,12 15.5,6'
const TORSO = '15,17 25,17 26,31 14,31'
const ARMS: Record<Pose, [string, string]> = {
  stand: ['15,17 12,19 11,31 13.5,31 15,22', '25,17 28,19 29,31 26.5,31 25,22'],
  walk: ['15,17 12,19 9.5,29 12,30 15,22', '25,17 28,19 30.5,29 28,30 25,22'],
  wave: ['15,17 12,19 11,31 13.5,31 15,22', '25,17 28,16 31.5,4 34,5.5 29.5,19.5 25,22'],
  point: ['15,17 12,19 11,31 13.5,31 15,22', '25,17 37.5,16.5 37.5,20 26,22.5'],
}
const LEGS: Record<Pose, [string, string]> = {
  stand: ['14.5,31 19.5,31 19,55 15,55', '20.5,31 25.5,31 25,55 21,55'],
  walk: ['15,31 19.5,31 16.5,55 12.5,55', '20.5,31 25,31 27.5,55 23.5,55'],
  wave: ['14.5,31 19.5,31 19,55 15,55', '20.5,31 25.5,31 25,55 21,55'],
  point: ['14.5,31 19.5,31 17.5,55 13.5,55', '20.5,31 25.5,31 26.5,55 22.5,55'],
}

/** クリスタルの人形。地図の現在地、はじめての画面、プロフィールで使う。 */
export function CrystalFigure({ look, size = 48, glow = true }: { look: AvatarLook; size?: number; glow?: boolean }) {
  const id = useId().replace(/:/g, '')
  const t = TINTS.find((x) => x.id === look.tint) ?? TINTS[0]
  const [armL, armR] = ARMS[look.pose]
  const [legL, legR] = LEGS[look.pose]
  const facet = { stroke: t.light, strokeWidth: 0.6, strokeLinejoin: 'round' as const }
  return (
    <svg
      className={`crystal ${glow ? 'crystal--glow' : ''}`}
      viewBox="0 0 40 64"
      width={size * 0.625}
      height={size}
      style={{ ['--crystal-glow' as string]: t.glow }}
      aria-hidden
    >
      <defs>
        <linearGradient id={`a${id}`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={t.light} stopOpacity=".95" />
          <stop offset=".45" stopColor={t.mid} stopOpacity=".75" />
          <stop offset="1" stopColor={t.dark} stopOpacity=".85" />
        </linearGradient>
        <linearGradient id={`b${id}`} x1="1" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor={t.mid} stopOpacity=".55" />
          <stop offset="1" stopColor={t.dark} stopOpacity=".9" />
        </linearGradient>
      </defs>
      {/* 台座 */}
      <polygon points="5,56 35,56 37,60 3,60" fill={`url(#b${id})`} {...facet} />
      <polygon points="5,56 35,56 33,54.5 7,54.5" fill={t.light} opacity=".55" />
      {/* 体 */}
      <polygon points={legL} fill={`url(#b${id})`} {...facet} />
      <polygon points={legR} fill={`url(#a${id})`} {...facet} />
      <polygon points={TORSO} fill={`url(#a${id})`} {...facet} />
      <polyline points="20,17 20,31" stroke={t.light} strokeWidth=".5" opacity=".6" fill="none" />
      <polyline points="15,24 25,24" stroke={t.light} strokeWidth=".5" opacity=".4" fill="none" />
      <polygon points={armL} fill={`url(#b${id})`} {...facet} />
      <polygon points={armR} fill={`url(#a${id})`} {...facet} />
      <polygon points={HEAD} fill={`url(#a${id})`} {...facet} />
      <polygon points="20,3 24.5,6 20,9 15.5,6" fill={t.light} opacity=".7" />
      <polyline points="16,8 18,6" stroke="#fff" strokeWidth=".9" strokeLinecap="round" opacity=".9" />
    </svg>
  )
}
