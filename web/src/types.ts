import type { Timestamp } from 'firebase/firestore'

export type Drop = {
  id: string
  authorId: string
  authorName: string
  teaser: string
  lat: number
  lng: number
  radius: number
  hasPhoto: boolean
  openCount: number
  likeCount?: number
  commentCount?: number
  createdAt: Timestamp | null
}

export type OpenedEntry = {
  id: string
  teaser: string
  authorName: string
  authorId?: string
  lat: number
  lng: number
  radius: number
  openedAt: Timestamp | null
  openedDistance: number
}

export type PlacedEntry = {
  id: string
  teaser: string
  lat: number
  lng: number
  radius: number
  createdAt: Timestamp | null
}

export type Profile = {
  name?: string
  placed?: number
  opened?: number
  opensReceived?: number
  likesReceived?: number
  comments?: number
}

export type Comment = {
  id: string
  authorId: string
  authorName: string
  text: string
  createdAt: Timestamp | null
}

export type Notice = {
  id: string
  type: 'open' | 'comment' | 'like'
  dropId: string
  teaser: string
  actorId: string
  actorName: string
  text?: string
  createdAt: Timestamp | null
}

/** 経験値とレベル。ひらく（歩く）ことを一番高く評価する。 */
export function levelOf(p: Profile | null | undefined) {
  const xp = (p?.opened ?? 0) * 30 + (p?.placed ?? 0) * 15 + (p?.opensReceived ?? 0) * 10 + (p?.likesReceived ?? 0) * 3 + (p?.comments ?? 0) * 2
  let level = 1
  let need = 50
  let rest = xp
  while (rest >= need) {
    rest -= need
    level += 1
    need = Math.round(need * 1.35)
  }
  return { xp, level, progress: rest / need, toNext: need - rest }
}
