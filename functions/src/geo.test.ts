import { test } from "node:test";
import assert from "node:assert/strict";
import { distanceMeters, isWithinRadius, isValidLatLng } from "./geo";

test("早稲田駅から大隈講堂はおよそ 500m 前後", () => {
  // 東京メトロ早稲田駅 と 大隈講堂 の概算座標
  const d = distanceMeters(35.7058, 139.7215, 35.7090, 139.7222);
  assert.ok(d > 300 && d < 450, `distance=${d}`);
});

test("同じ地点は 0m", () => {
  assert.equal(distanceMeters(35.7, 139.7, 35.7, 139.7), 0);
});

test("緯度 0.001 度はおよそ 111m", () => {
  const d = distanceMeters(35.0, 139.0, 35.001, 139.0);
  assert.ok(Math.abs(d - 111.2) < 1, `distance=${d}`);
});

test("精度の許容は最大 30m まで", () => {
  assert.equal(isWithinRadius(120, 100, 25), true);
  assert.equal(isWithinRadius(131, 100, 500), false);
  assert.equal(isWithinRadius(100, 100, undefined), true);
  assert.equal(isWithinRadius(101, 100, -50), false);
});

test("座標の検証", () => {
  assert.equal(isValidLatLng(35.7, 139.7), true);
  assert.equal(isValidLatLng(91, 0), false);
  assert.equal(isValidLatLng("35", 139), false);
  assert.equal(isValidLatLng(NaN, 139), false);
});

test("近い2点は同じマス目、離れた2点は別のマス目", async () => {
  const { cellOf } = await import("./geo");
  assert.equal(cellOf(35.70881, 139.72221).id, cellOf(35.70885, 139.72225).id);
  assert.notEqual(cellOf(35.7088, 139.7222).id, cellOf(35.7098, 139.7222).id);
  const c = cellOf(35.7088, 139.7222);
  assert.ok(Math.abs(c.lat - 35.7088) < 0.0003 && Math.abs(c.lng - 139.7222) < 0.0004);
});
