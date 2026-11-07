import { initializeApp } from 'firebase/app'
import { connectAuthEmulator, getAuth } from 'firebase/auth'
import { connectFirestoreEmulator, getFirestore } from 'firebase/firestore'
import { connectFunctionsEmulator, getFunctions, httpsCallable } from 'firebase/functions'
import { connectStorageEmulator, getStorage } from 'firebase/storage'

const env = import.meta.env
export const usingEmulators = env.VITE_USE_EMULATORS === 'true'

const projectId = env.VITE_FIREBASE_PROJECT_ID || 'demo-kokodake'

export const app = initializeApp({
  apiKey: env.VITE_FIREBASE_API_KEY || 'demo-api-key',
  authDomain: env.VITE_FIREBASE_AUTH_DOMAIN || `${projectId}.firebaseapp.com`,
  projectId,
  storageBucket: env.VITE_FIREBASE_STORAGE_BUCKET || `${projectId}.appspot.com`,
  appId: env.VITE_FIREBASE_APP_ID || undefined,
})

export const auth = getAuth(app)
export const db = getFirestore(app)
export const storage = getStorage(app)
export const functions = getFunctions(app, 'asia-northeast1')

if (usingEmulators) {
  const host = location.hostname
  connectAuthEmulator(auth, `http://${host}:9099`, { disableWarnings: true })
  connectFirestoreEmulator(db, host, 8080)
  connectStorageEmulator(storage, host, 9199)
  connectFunctionsEmulator(functions, host, 5001)
}

export type CreateDropInput = {
  teaser: string
  body: string
  radius: number
  lat: number
  lng: number
  authorName: string
  photoPath: string | null
}

export type OpenDropResult =
  | { status: 'too_far'; distance: number; radius: number }
  | { status: 'opened'; distance: number | null; radius: number; body: string; photo: string | null }

export const callCreateDrop = httpsCallable<CreateDropInput, { id: string }>(functions, 'createDrop')
export const callOpenDrop = httpsCallable<
  { dropId: string; lat?: number; lng?: number; accuracy?: number },
  OpenDropResult
>(functions, 'openDrop')
export const callDeleteDrop = httpsCallable<{ dropId: string }, { ok: boolean }>(functions, 'deleteDrop')
