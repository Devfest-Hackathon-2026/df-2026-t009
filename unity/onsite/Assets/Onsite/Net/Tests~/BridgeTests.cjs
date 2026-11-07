// ブラウザーを起動せず、Unity と DOM の境界を検証する。
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const assert = require("node:assert/strict");

const messages = [];
const library = {};
const geo = { success: null, error: null, options: null, stopped: [], sequence: 0 };
const inputs = [];
const listeners = new Map();
let imageFails = false;
let revoked = 0;
let lastCanvas;
let clicked = 0;
const document = {
  body: {
    appendChild(input) { input.parentNode = this; inputs.push(input); },
    removeChild(input) { input.parentNode = null; inputs.splice(inputs.indexOf(input), 1); }
  },
  createElement(tag) {
    if (tag === "input") return {
      style: {}, files: [], setAttribute(key, value) { this[key] = value; },
      click() { clicked++; }
    };
    assert.equal(tag, "canvas");
    lastCanvas = {
      getContext() { return { fillRect() {}, drawImage() {} }; },
      toDataURL(type, quality) {
        assert.equal(type, "image/jpeg");
        assert.equal(quality, 0.82);
        return "data:image/jpeg;base64,/9j/2Q==";
      }
    };
    return lastCanvas;
  }
};
const sandbox = {
  LibraryManager: { library },
  mergeInto(target, source) { Object.assign(target, source); },
  SendMessage(name, method, value) { messages.push({ name, method, value }); },
  navigator: { geolocation: {
    watchPosition(success, error, options) {
      geo.success = success; geo.error = error; geo.options = options;
      return geo.sequence++;
    },
    clearWatch(id) { geo.stopped.push(id); }
  }},
  document,
  window: {
    addEventListener(name, callback) { listeners.set(name, callback); },
    removeEventListener(name, callback) { if (listeners.get(name) === callback) listeners.delete(name); }
  },
  URL: { createObjectURL() { return "blob:photo"; }, revokeObjectURL() { revoked++; } },
  Image: class {
    constructor() { this.naturalWidth = 3200; this.naturalHeight = 2400; }
    set src(value) { if (imageFails) this.onerror(); else this.onload(); }
  },
  setTimeout(callback) { callback(); return 1; },
  clearTimeout() {}
};
const bridgePath = path.resolve(__dirname, "../../../Plugins/WebGL/OnsiteBridge.jslib");
vm.runInNewContext(fs.readFileSync(bridgePath, "utf8"), sandbox, { filename: bridgePath });
sandbox.OnsiteBridge = library.$OnsiteBridge;
const state = sandbox.OnsiteBridge;

library.Onsite_StartLocation();
assert.equal(geo.options.enableHighAccuracy, true);
assert.equal(geo.options.maximumAge, 0);
geo.success({ coords: { latitude: 35.25, longitude: 139.5, accuracy: 12 } });
assert.deepEqual(messages.pop(), { name: "OnsiteBridge", method: "OnGeo", value: "35.25,139.5,12" });
geo.error({ code: 1, message: "権限がありません" });
assert.equal(messages.pop().method, "OnGeoError");
const oldCallback = geo.success;
library.Onsite_StartLocation();
assert.deepEqual(geo.stopped, [0]);
oldCallback({ coords: { latitude: 0, longitude: 0, accuracy: 1 } });
assert.equal(messages.length, 0);
library.Onsite_StopLocation();
assert.deepEqual(geo.stopped, [0, 1]);
assert.equal(state.watchId, null);
sandbox.navigator.geolocation = null;
library.Onsite_StartLocation();
assert.equal(messages.pop().method, "OnGeoError");

library.Onsite_SelectPhoto();
assert.equal(inputs.length, 1);
assert.equal(inputs[0].accept, "image/*");
assert.equal(inputs[0].capture, "environment");
assert.equal(clicked, 1);
inputs[0].files = [{ type: "image/png" }];
inputs[0].onchange();
assert.equal(lastCanvas.width, 1600);
assert.equal(lastCanvas.height, 1200);
assert.deepEqual(messages.pop(), { name: "OnsiteBridge", method: "OnPhoto", value: "/9j/2Q==" });
assert.equal(inputs.length, 0);
assert.equal(listeners.size, 0);
assert.equal(revoked, 1);
assert.equal(state.photo, null);

library.Onsite_SelectPhoto();
inputs[0].oncancel();
assert.equal(messages.pop().value, "");
assert.equal(inputs.length, 0);
library.Onsite_SelectPhoto();
listeners.get("focus")();
assert.equal(messages.pop().method, "OnPhoto");
assert.equal(inputs.length, 0);

library.Onsite_SelectPhoto();
library.Onsite_SelectPhoto();
assert.equal(inputs.length, 1);
library.Onsite_CancelPhoto();
assert.equal(inputs.length, 0);
assert.equal(messages.length, 0);
assert.equal(listeners.size, 0);

imageFails = true;
library.Onsite_SelectPhoto();
inputs[0].files = [{}];
inputs[0].onchange();
assert.equal(messages.pop().method, "OnPhotoError");
assert.equal(revoked, 2);
assert.equal(inputs.length, 0);
assert.equal(state.photo, null);
console.log("WebGL bridge tests passed: location, photos, errors, cancellation and cleanup");