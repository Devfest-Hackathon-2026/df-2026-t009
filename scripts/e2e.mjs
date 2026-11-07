// 本番の ここだけ を、匿名ユーザー2人（A=投稿者、B=歩く人）で試す。
// 使い方: node scripts/e2e.mjs web/.env.local   （本番の Firebase に一時的な投稿を作り、最後に消す）
import { readFileSync } from 'node:fs'

const env = Object.fromEntries(
  readFileSync(process.argv[2], 'utf8')
    .split(/\r?\n/)
    .filter((l) => /^VITE_[A-Z_]+=/.test(l))
    .map((l) => [l.split('=')[0], l.slice(l.indexOf('=') + 1)]),
)
const KEY = env.VITE_FIREBASE_API_KEY
const PROJECT = env.VITE_FIREBASE_PROJECT_ID
const FN = `https://asia-northeast1-${PROJECT}.cloudfunctions.net`
const FS = `https://firestore.googleapis.com/v1/projects/${PROJECT}/databases/(default)/documents`
const ORIGIN = { Origin: `https://${PROJECT}.web.app`, Referer: `https://${PROJECT}.web.app/` }

let failures = 0
const check = (name, ok, extra = '') => {
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${extra ? '  ' + extra : ''}`)
  if (!ok) failures++
}

async function signUp() {
  const r = await fetch(`https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${KEY}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...ORIGIN },
    body: JSON.stringify({ returnSecureToken: true }),
  })
  const j = await r.json()
  if (!j.idToken) throw new Error('signUp failed: ' + JSON.stringify(j))
  return { token: j.idToken, uid: j.localId }
}

async function call(user, name, data) {
  const r = await fetch(`${FN}/${name}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${user.token}`, ...ORIGIN },
    body: JSON.stringify({ data }),
  })
  const j = await r.json().catch(() => ({}))
  return { status: r.status, result: j.result, error: j.error }
}

async function get(user, path) {
  const r = await fetch(`${FS}/${path}`, { headers: { Authorization: `Bearer ${user.token}` } })
  return { status: r.status, body: await r.json().catch(() => ({})) }
}

async function list(user, path) {
  const r = await fetch(`${FS}/${path}?pageSize=50`, { headers: { Authorization: `Bearer ${user.token}` } })
  return { status: r.status, body: await r.json().catch(() => ({})) }
}

// 大隈講堂のあたり
const P = { lat: 35.7088, lng: 139.7222 }
const far = { lat: P.lat + 0.009, lng: P.lng } // 約1km北
const near = { lat: P.lat + 0.0005, lng: P.lng } // 約55m北

const A = await signUp()
const B = await signUp()

const created = await call(A, 'createDrop', {
  teaser: '【自動テスト】すぐ消えます',
  body: 'テスト本文',
  authorName: 'テストA',
  radius: 100,
  ...P,
})
check('A が投稿できる', created.status === 200 && created.result?.id, JSON.stringify(created.error ?? ''))
const id = created.result?.id

try {
  const tooFar = await call(B, 'openDrop', { dropId: id, ...far, accuracy: 10, name: 'テストB' })
  check('B は 1km 離れているとひらけない', tooFar.result?.status === 'too_far' && !tooFar.result?.body, `距離 ${tooFar.result?.distance}m`)

  const privateRead = await get(B, `drops/${id}/private/content`)
  check('B は本文を Firestore から直接読めない', privateRead.status === 403, `HTTP ${privateRead.status}`)

  const commentsBefore = await list(B, `drops/${id}/comments`)
  check('ひらく前の B はコメントを読めない', commentsBefore.status === 403, `HTTP ${commentsBefore.status}`)

  const commentEarly = await call(B, 'addComment', { dropId: id, text: 'ずる', name: 'テストB' })
  check('ひらく前の B はコメントを書けない', commentEarly.error?.status === 'PERMISSION_DENIED', commentEarly.error?.status)

  const ok = await call(B, 'openDrop', { dropId: id, ...near, accuracy: 10, name: 'テストB' })
  check('B は 55m まで近づくとひらける', ok.result?.status === 'opened' && ok.result?.body === 'テスト本文', `firstTime=${ok.result?.firstTime}`)
  check('はじめてひらいたことが分かる', ok.result?.firstTime === true)

  const again = await call(B, 'openDrop', { dropId: id, ...far, name: 'テストB' })
  check('一度ひらいたら遠くからでも読み返せる', again.result?.status === 'opened' && again.result?.firstTime === false)

  const c = await call(B, 'addComment', { dropId: id, text: '来たよ！', name: 'テストB' })
  check('ひらいた B はコメントを書ける', c.status === 200 && c.result?.id, JSON.stringify(c.error ?? ''))

  const like = await call(B, 'toggleLike', { dropId: id, name: 'テストB' })
  check('ひらいた B はいいねできる', like.result?.liked === true)

  const commentsB = await list(B, `drops/${id}/comments`)
  check('B はコメントを読める', commentsB.status === 200 && (commentsB.body.documents?.length ?? 0) === 1)
  const commentsA = await list(A, `drops/${id}/comments`)
  check('投稿者 A もコメントを読める', commentsA.status === 200 && (commentsA.body.documents?.length ?? 0) === 1)

  const C = await signUp()
  const commentsC = await list(C, `drops/${id}/comments`)
  check('ひらいていない第三者 C はコメントを読めない', commentsC.status === 403, `HTTP ${commentsC.status}`)

  const drop = await get(C, `drops/${id}`)
  const f = drop.body.fields ?? {}
  check(
    '公開の数字が増えている（ひらいた1・いいね1・コメント1）',
    f.openCount?.integerValue === '1' && f.likeCount?.integerValue === '1' && f.commentCount?.integerValue === '1',
  )

  const notices = await list(A, `users/${A.uid}/notifications`)
  const types = (notices.body.documents ?? []).map((d) => d.fields.type.stringValue).sort()
  check('A にお知らせが3件届く（open・comment・like）', types.join(',') === 'comment,like,open', types.join(','))

  const otherNotices = await list(B, `users/${A.uid}/notifications`)
  check('B は A のお知らせを読めない', otherNotices.status === 403, `HTTP ${otherNotices.status}`)

  const prof = await get(C, `profiles/${A.uid}`)
  const pf = prof.body.fields ?? {}
  check('A のプロフィールが公開されている', pf.name?.stringValue === 'テストA' && pf.opensReceived?.integerValue === '1')

  const unlike = await call(B, 'toggleLike', { dropId: id, name: 'テストB' })
  check('いいねを取り消せる', unlike.result?.liked === false)

  const delByB = await call(B, 'deleteDrop', { dropId: id })
  check('B は A の投稿を消せない', delByB.error?.status === 'PERMISSION_DENIED')
} finally {
  const del = await call(A, 'deleteDrop', { dropId: id })
  check('A は自分の投稿を消せる', del.result?.ok === true)
  const gone = await list(A, `drops/${id}/comments`)
  check('消すとコメントも消える', !(gone.body.documents?.length > 0))
}

console.log(failures === 0 ? '\nALL PASS' : `\n${failures} FAILED`)
process.exit(failures ? 1 : 0)
