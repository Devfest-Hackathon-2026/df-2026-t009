import { onAuthStateChanged, signInAnonymously, type User } from 'firebase/auth'
import { collection, limit, onSnapshot, orderBy, query } from 'firebase/firestore'
import { useEffect, useState } from 'react'
import { auth, db } from './firebase'
import type { Drop, OpenedEntry, PlacedEntry } from './types'

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

export function useMyLog(uid: string | undefined) {
  const [opened, setOpened] = useState<OpenedEntry[]>([])
  const [placed, setPlaced] = useState<PlacedEntry[]>([])
  useEffect(() => {
    if (!uid) return
    const u1 = onSnapshot(query(collection(db, `users/${uid}/opened`), orderBy('openedAt', 'desc')), (s) =>
      setOpened(s.docs.map((d) => ({ id: d.id, ...d.data() }) as OpenedEntry)),
    )
    const u2 = onSnapshot(query(collection(db, `users/${uid}/placed`), orderBy('createdAt', 'desc')), (s) =>
      setPlaced(s.docs.map((d) => ({ id: d.id, ...d.data() }) as PlacedEntry)),
    )
    return () => {
      u1()
      u2()
    }
  }, [uid])
  return { opened, placed }
}
