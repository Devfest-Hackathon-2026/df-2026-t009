// Unity を起動せず、振動パターンの受け渡しを確認する。
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const assert = require("node:assert/strict");
const library = {};
const vibrations = [];
const heap = new Int32Array(64);
const sandbox = {
  LibraryManager: { library },
  mergeInto(target, source) { Object.assign(target, source); },
  HEAP32: heap,
  navigator: { vibrate(value) { vibrations.push(Array.isArray(value) ? Array.from(value) : value); } }
};
const bridge = path.resolve(__dirname, "../../../Plugins/WebGL/OnsiteBridge.jslib");
vm.runInNewContext(fs.readFileSync(bridge, "utf8"), sandbox, { filename: bridge });
const patterns = [[8], [14], [30, 60, 30], [20, 40, 20, 40, 80], [40, 60, 40, 60, 120], [60, 40, 60]];
for (const pattern of patterns) {
  heap.fill(0); heap.set(pattern, 3);
  library.Onsite_VibratePattern(12, pattern.length);
  assert.deepEqual(vibrations.pop(), pattern);
}
heap.set([-1, 9000, 14], 3);
library.Onsite_VibratePattern(12, 3);
assert.deepEqual(vibrations.pop(), [0, 1000, 14]);
for (const [ptr, len] of [[0, 1], [13, 1], [12, 0], [12, 33], [252, 2], [-4, 1]]) {
  library.Onsite_VibratePattern(ptr, len);
  assert.equal(vibrations.length, 0);
}
library.Onsite_Vibrate(0);
assert.equal(vibrations.pop(), 0);
sandbox.navigator.vibrate = undefined;
assert.doesNotThrow(() => library.Onsite_VibratePattern(12, 1));
assert.doesNotThrow(() => library.Onsite_Vibrate(8));
sandbox.navigator.vibrate = () => { throw new Error("unsupported"); };
assert.doesNotThrow(() => library.Onsite_VibratePattern(12, 1));
assert.doesNotThrow(() => library.Onsite_Vibrate(8));
console.log("Haptics bridge tests passed: six patterns, heap bounds, opt-out cancellation and unsupported devices");
