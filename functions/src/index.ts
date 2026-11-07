import { initializeApp } from "firebase-admin/app";
import { getFirestore, FieldValue, Timestamp, type Transaction, type WriteBatch } from "firebase-admin/firestore";
import { getStorage } from "firebase-admin/storage";
import { onCall, HttpsError } from "firebase-functions/v2/https";
import { setGlobalOptions } from "firebase-functions/v2";
import { ALLOWED_RADII, distanceMeters, isValidLatLng, isWithinRadius } from "./geo";

initializeApp();
// 呼び出し可否は関数の中で Firebase Auth を確かめる。Cloud Run の手前では誰でも呼べるようにする（callable の標準）。
setGlobalOptions({ region: "asia-northeast1", maxInstances: 5, invoker: "public" });

const db = getFirestore();

const LIMITS = { teaser: 60, body: 600, name: 20, comment: 200 };
const MAX_PHOTO_BYTES = 3 * 1024 * 1024;

function requireUid(auth: { uid: string } | undefined): string {
  if (!auth) throw new HttpsError("unauthenticated", "ログインが必要です");
  return auth.uid;
}

function cleanText(v: unknown, max: number, field: string, required: boolean): string {
  if (v === undefined || v === null || v === "") {
    if (required) throw new HttpsError("invalid-argument", `${field} を入力してください`);
    return "";
  }
  if (typeof v !== "string") throw new HttpsError("invalid-argument", `${field} が不正です`);
  const s = v.trim();
  if (required && s.length === 0) throw new HttpsError("invalid-argument", `${field} を入力してください`);
  if ([...s].length > max) throw new HttpsError("invalid-argument", `${field} は ${max} 文字以内にしてください`);
  return s;
}

function cleanDropId(v: unknown): string {
  if (typeof v !== "string" || !/^[A-Za-z0-9]{1,40}$/.test(v)) {
    throw new HttpsError("invalid-argument", "投稿 ID が不正です");
  }
  return v;
}

/** 名前は任意。空なら「名無し」にする（プロフィールの表示用）。 */
function cleanName(v: unknown): string {
  try {
    return cleanText(v, LIMITS.name, "ニックネーム", false) || "名無し";
  } catch {
    return "名無し";
  }
}

const profileRef = (uid: string) => db.doc(`profiles/${uid}`);

/** 公開プロフィールの数字を増減する。名前も最新にしておく。 */
function bumpProfile(
  tx: Transaction | WriteBatch,
  uid: string,
  fields: Partial<Record<"placed" | "opened" | "opensReceived" | "likesReceived" | "comments", number>>,
  name?: string,
) {
  const data: Record<string, unknown> = { updatedAt: Timestamp.now() };
  if (name) data.name = name;
  for (const [k, n] of Object.entries(fields)) data[k] = FieldValue.increment(n as number);
  (tx as WriteBatch).set(profileRef(uid), data, { merge: true });
}

type NotificationType = "open" | "comment" | "like";

/** 投稿者へのお知らせ。自分の操作では送らない。 */
function notify(
  tx: Transaction,
  to: string,
  from: string,
  n: { type: NotificationType; dropId: string; teaser: string; actorName: string; text?: string },
) {
  if (to === from) return;
  tx.set(db.collection(`users/${to}/notifications`).doc(), { ...n, actorId: from, createdAt: Timestamp.now() });
}

/** ひらいた人か投稿者だけが、コメント・いいねできる。 */
async function requireUnlocked(tx: Transaction, uid: string, dropId: string) {
  const dropRef = db.doc(`drops/${dropId}`);
  const [dropSnap, openedSnap] = await Promise.all([tx.get(dropRef), tx.get(db.doc(`users/${uid}/opened/${dropId}`))]);
  if (!dropSnap.exists) throw new HttpsError("not-found", "投稿が見つかりません");
  const drop = dropSnap.data()!;
  if (!openedSnap.exists && drop.authorId !== uid) {
    throw new HttpsError("permission-denied", "この場所でひらいた人だけが反応できます");
  }
  return { dropRef, drop };
}

/**
 * 投稿を作る。公開部分（予告・位置・範囲）と、非公開部分（本文・写真）を分けて保存する。
 * 非公開部分はクライアントから直接読めない（firestore.rules で拒否）。
 */
export const createDrop = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const d = req.data ?? {};

  const teaser = cleanText(d.teaser, LIMITS.teaser, "予告", true);
  const body = cleanText(d.body, LIMITS.body, "本文", false);
  const authorName = cleanText(d.authorName, LIMITS.name, "ニックネーム", true);
  const radius = Number(d.radius);
  if (!(ALLOWED_RADII as readonly number[]).includes(radius)) {
    throw new HttpsError("invalid-argument", "範囲は 100m・300m・500m から選んでください");
  }
  if (!isValidLatLng(d.lat, d.lng)) throw new HttpsError("invalid-argument", "場所が不正です");

  let photoPath: string | null = null;
  if (d.photoPath) {
    if (typeof d.photoPath !== "string" || !d.photoPath.startsWith(`uploads/${uid}/`) || d.photoPath.includes("..")) {
      throw new HttpsError("invalid-argument", "写真が不正です");
    }
    const [meta] = await getStorage().bucket().file(d.photoPath).getMetadata().catch(() => {
      throw new HttpsError("invalid-argument", "写真が見つかりません");
    });
    if (Number(meta.size) > MAX_PHOTO_BYTES || !String(meta.contentType).startsWith("image/")) {
      throw new HttpsError("invalid-argument", "写真が大きすぎるか、画像ではありません");
    }
    photoPath = d.photoPath;
  }
  if (!body && !photoPath) throw new HttpsError("invalid-argument", "本文か写真のどちらかは必要です");

  const ref = db.collection("drops").doc();
  const now = Timestamp.now();
  const batch = db.batch();
  batch.set(ref, {
    authorId: uid,
    authorName,
    teaser,
    lat: d.lat,
    lng: d.lng,
    radius,
    hasPhoto: photoPath !== null,
    openCount: 0,
    likeCount: 0,
    commentCount: 0,
    createdAt: now,
  });
  batch.set(ref.collection("private").doc("content"), { body, photoPath });
  batch.set(db.doc(`users/${uid}/placed/${ref.id}`), { teaser, lat: d.lat, lng: d.lng, radius, createdAt: now });
  bumpProfile(batch, uid, { placed: 1 }, authorName);
  await batch.commit();
  return { id: ref.id };
});

/**
 * 投稿を開く。送られてきた現在地が範囲内なら本文と写真を返す。
 * 一度開いた投稿と自分の投稿は、どこからでも読み返せる。
 *
 * 注意: 現在地は端末が申告する値なので、GPS の偽装までは防げない。
 */
export const openDrop = onCall({ memory: "512MiB" }, async (req) => {
  const uid = requireUid(req.auth);
  const { lat, lng, accuracy } = req.data ?? {};
  const dropId = cleanDropId(req.data?.dropId);
  const name = cleanName(req.data?.name);

  const dropRef = db.doc(`drops/${dropId}`);
  const openedRef = db.doc(`users/${uid}/opened/${dropId}`);

  const result = await db.runTransaction(async (tx) => {
    const [dropSnap, openedSnap] = await Promise.all([tx.get(dropRef), tx.get(openedRef)]);
    if (!dropSnap.exists) throw new HttpsError("not-found", "投稿が見つかりません");
    const drop = dropSnap.data()!;

    let distance: number | null = null;
    if (isValidLatLng(lat, lng)) distance = distanceMeters(lat, lng, drop.lat, drop.lng);

    const alreadyAllowed = openedSnap.exists || drop.authorId === uid;
    let firstTime = false;
    if (!alreadyAllowed) {
      if (distance === null) throw new HttpsError("invalid-argument", "現在地が取得できていません");
      const acc = typeof accuracy === "number" && Number.isFinite(accuracy) ? accuracy : undefined;
      if (!isWithinRadius(distance, drop.radius, acc)) {
        return { status: "too_far" as const, distance: Math.round(distance), radius: drop.radius };
      }
      firstTime = true;
      tx.set(openedRef, {
        teaser: drop.teaser,
        authorName: drop.authorName,
        authorId: drop.authorId,
        lat: drop.lat,
        lng: drop.lng,
        radius: drop.radius,
        openedAt: Timestamp.now(),
        openedDistance: Math.round(distance),
      });
      tx.update(dropRef, { openCount: FieldValue.increment(1) });
      bumpProfile(tx, uid, { opened: 1 }, name);
      bumpProfile(tx, drop.authorId, { opensReceived: 1 });
      notify(tx, drop.authorId, uid, { type: "open", dropId, teaser: drop.teaser, actorName: name });
    }
    return {
      status: "opened" as const,
      firstTime,
      distance: distance === null ? null : Math.round(distance),
      radius: drop.radius,
    };
  });

  if (result.status === "too_far") return result;

  const content = (await db.doc(`drops/${dropId}/private/content`).get()).data() ?? {};
  let photo: string | null = null;
  if (content.photoPath) {
    const file = getStorage().bucket().file(content.photoPath);
    const [[buf], [meta]] = await Promise.all([file.download(), file.getMetadata()]);
    photo = `data:${meta.contentType ?? "image/jpeg"};base64,${buf.toString("base64")}`;
  }
  return { ...result, body: content.body ?? "", photo };
});

/** その場所でひらいた人だけが書けるコメント。読めるのも、ひらいた人と投稿者だけ（firestore.rules）。 */
export const addComment = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const dropId = cleanDropId(req.data?.dropId);
  const text = cleanText(req.data?.text, LIMITS.comment, "コメント", true);
  const name = cleanName(req.data?.name);

  return db.runTransaction(async (tx) => {
    const { dropRef, drop } = await requireUnlocked(tx, uid, dropId);
    const ref = dropRef.collection("comments").doc();
    tx.set(ref, { authorId: uid, authorName: name, text, createdAt: Timestamp.now() });
    tx.update(dropRef, { commentCount: FieldValue.increment(1) });
    bumpProfile(tx, uid, { comments: 1 }, name);
    notify(tx, drop.authorId, uid, { type: "comment", dropId, teaser: drop.teaser, actorName: name, text });
    return { id: ref.id };
  });
});

/** いいね（ひらいた人だけ）。もう一度押すと取り消す。 */
export const toggleLike = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const dropId = cleanDropId(req.data?.dropId);
  const name = cleanName(req.data?.name);

  return db.runTransaction(async (tx) => {
    const likedRef = db.doc(`users/${uid}/liked/${dropId}`);
    const likedSnap = await tx.get(likedRef);
    const { dropRef, drop } = await requireUnlocked(tx, uid, dropId);
    if (likedSnap.exists) {
      tx.delete(likedRef);
      tx.update(dropRef, { likeCount: FieldValue.increment(-1) });
      bumpProfile(tx, drop.authorId, { likesReceived: -1 });
      return { liked: false };
    }
    tx.set(likedRef, { createdAt: Timestamp.now() });
    tx.update(dropRef, { likeCount: FieldValue.increment(1) });
    bumpProfile(tx, drop.authorId, { likesReceived: 1 });
    notify(tx, drop.authorId, uid, { type: "like", dropId, teaser: drop.teaser, actorName: name });
    return { liked: true };
  });
});

/** 自分の投稿を消す（公開部分・非公開部分・コメント・写真）。 */
export const deleteDrop = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const dropId = cleanDropId(req.data?.dropId);
  const dropRef = db.doc(`drops/${dropId}`);
  const snap = await dropRef.get();
  if (!snap.exists) return { ok: true };
  if (snap.data()!.authorId !== uid) throw new HttpsError("permission-denied", "自分の投稿だけ消せます");
  const content = (await dropRef.collection("private").doc("content").get()).data();
  if (content?.photoPath) await getStorage().bucket().file(content.photoPath).delete().catch(() => undefined);
  await db.recursiveDelete(dropRef);
  const batch = db.batch();
  batch.delete(db.doc(`users/${uid}/placed/${dropId}`));
  bumpProfile(batch, uid, { placed: -1 });
  await batch.commit();
  return { ok: true };
});
