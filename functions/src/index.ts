import { initializeApp } from "firebase-admin/app";
import { getFirestore, FieldValue, Timestamp } from "firebase-admin/firestore";
import { getStorage } from "firebase-admin/storage";
import { onCall, HttpsError } from "firebase-functions/v2/https";
import { setGlobalOptions } from "firebase-functions/v2";
import { ALLOWED_RADII, distanceMeters, isValidLatLng, isWithinRadius } from "./geo";

initializeApp();
setGlobalOptions({ region: "asia-northeast1", maxInstances: 5 });

const db = getFirestore();

const LIMITS = { teaser: 60, body: 600, name: 20 };
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
    createdAt: now,
  });
  batch.set(ref.collection("private").doc("content"), { body, photoPath });
  batch.set(db.doc(`users/${uid}/placed/${ref.id}`), { teaser, lat: d.lat, lng: d.lng, radius, createdAt: now });
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
  const { dropId, lat, lng, accuracy } = req.data ?? {};
  if (typeof dropId !== "string" || !/^[A-Za-z0-9]{1,40}$/.test(dropId)) {
    throw new HttpsError("invalid-argument", "投稿 ID が不正です");
  }

  const dropRef = db.doc(`drops/${dropId}`);
  const openedRef = db.doc(`users/${uid}/opened/${dropId}`);

  const result = await db.runTransaction(async (tx) => {
    const [dropSnap, openedSnap] = await Promise.all([tx.get(dropRef), tx.get(openedRef)]);
    if (!dropSnap.exists) throw new HttpsError("not-found", "投稿が見つかりません");
    const drop = dropSnap.data()!;

    let distance: number | null = null;
    if (isValidLatLng(lat, lng)) distance = distanceMeters(lat, lng, drop.lat, drop.lng);

    const alreadyAllowed = openedSnap.exists || drop.authorId === uid;
    if (!alreadyAllowed) {
      if (distance === null) throw new HttpsError("invalid-argument", "現在地が取得できていません");
      const acc = typeof accuracy === "number" && Number.isFinite(accuracy) ? accuracy : undefined;
      if (!isWithinRadius(distance, drop.radius, acc)) {
        return { status: "too_far" as const, distance: Math.round(distance), radius: drop.radius };
      }
      tx.set(openedRef, {
        teaser: drop.teaser,
        authorName: drop.authorName,
        lat: drop.lat,
        lng: drop.lng,
        radius: drop.radius,
        openedAt: Timestamp.now(),
        openedDistance: Math.round(distance),
      });
      tx.update(dropRef, { openCount: FieldValue.increment(1) });
    }
    return { status: "opened" as const, distance: distance === null ? null : Math.round(distance), radius: drop.radius };
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

/** 自分の投稿を消す（公開部分・非公開部分・写真）。 */
export const deleteDrop = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const dropId = req.data?.dropId;
  if (typeof dropId !== "string" || !/^[A-Za-z0-9]{1,40}$/.test(dropId)) {
    throw new HttpsError("invalid-argument", "投稿 ID が不正です");
  }
  const dropRef = db.doc(`drops/${dropId}`);
  const snap = await dropRef.get();
  if (!snap.exists) return { ok: true };
  if (snap.data()!.authorId !== uid) throw new HttpsError("permission-denied", "自分の投稿だけ消せます");
  const contentRef = dropRef.collection("private").doc("content");
  const content = (await contentRef.get()).data();
  if (content?.photoPath) await getStorage().bucket().file(content.photoPath).delete().catch(() => undefined);
  const batch = db.batch();
  batch.delete(contentRef);
  batch.delete(dropRef);
  batch.delete(db.doc(`users/${uid}/placed/${dropId}`));
  await batch.commit();
  return { ok: true };
});
