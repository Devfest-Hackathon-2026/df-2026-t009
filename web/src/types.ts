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
  createdAt: Timestamp | null
}

export type OpenedEntry = {
  id: string
  teaser: string
  authorName: string
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
