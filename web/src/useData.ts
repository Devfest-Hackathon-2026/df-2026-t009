import { onAuthStateChanged, signInAnonymously, type User } from 'firebase/auth'
import { collection, doc, limit, onSnapshot, orderBy, query } from 'firebase/firestore'
import { useEffect, useState } from 'react'
import { auth, db } from './firebase'
import type { Comment, Drop, Notice, OpenedEntry, PlacedEntry, Profile } from './types'

export function useAnonymousUser() {
  const [user, setUser] = useState<User | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(
    () =>
      onAuthStateChanged(auth, (u) => {
        if (u) setUser(u)
        else signInAnonymously(auth).catch((e) => setError(String(e?.message ?? e)))
      }),
    [],
  )
  return { user, error }
}

export function useDrops() {
  const [drops, setDrops] = useState<Drop[]>([])
  useEffect(() => {
    const q = query(collection(db, 'drops'), orderBy('createdAt', 'desc'), limit(300))
    return onSnapshot(q, (snap) => setDrops(snap.docs.map((d) => ({ id: d.id, ...d.data() }) as Drop)))
  }, [])
  return drops
}

function useOwnCollection<T>(uid: string | undefined, name: string, orderField: string) {
  const [items, setItems] = useState<T[]>([])
  useEffect(() => {
    if (!uid) return
    return onSnapshot(
      query(collection(db, `users/${uid}/${name}`), orderBy(orderField, 'desc'), limit(200)),
      (s) => setItems(s.docs.map((d) => ({ id: d.id, ...d.data() }) as T)),
      () => setItems([]),
    )
  }, [uid, name, orderField])
  return items
}

export function useMyLog(uid: string | undefined) {
  const opened = useOwnCollection<OpenedEntry>(uid, 'opened', 'openedAt')
  const placed = useOwnCollection<PlacedEntry>(uid, 'placed', 'createdAt')
  return { opened, placed }
}

export function useNotices(uid: string | undefined) {
  return useOwnCollection<Notice>(uid, 'notifications', 'createdAt')
}

export function useLikedIds(uid: string | undefined) {
  const liked = useOwnCollection<{ id: string }>(uid, 'liked', 'createdAt')
  return new Set(liked.map((l) => l.id))
}

export function useProfile(uid: string | null | undefined) {
  const [profile, setProfile] = useState<Profile | null>(null)
  useEffect(() => {
    if (!uid) return
    return onSnapshot(
      doc(db, `profiles/${uid}`),
      (s) => setProfile((s.data() as Profile) ?? {}),
      () => setProfile({}),
    )
  }, [uid])
  return profile
}

/** コメントは、ひらいた人と投稿者だけが読める（ルールで拒否されたら空のまま）。 */
export function useComments(dropId: string | null, enabled: boolean) {
  const [comments, setComments] = useState<Comment[]>([])
  useEffect(() => {
    if (!dropId || !enabled) return
    return onSnapshot(
      query(collection(db, `drops/${dropId}/comments`), orderBy('createdAt', 'asc'), limit(100)),
      (s) => setComments(s.docs.map((d) => ({ id: d.id, ...d.data() }) as Comment)),
      () => setComments([]),
    )
  }, [dropId, enabled])
  return comments
}
