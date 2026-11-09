mergeInto(LibraryManager.library, {
  $OnsiteBridge: {
    watchId: null,
    geoGeneration: 0,
    photo: null,

    send: function (method, value) {
      SendMessage("OnsiteBridge", method, value);
    },

    stopLocation: function () {
      OnsiteBridge.geoGeneration++;
      if (OnsiteBridge.watchId !== null && navigator.geolocation) {
        navigator.geolocation.clearWatch(OnsiteBridge.watchId);
      }
      OnsiteBridge.watchId = null;
    },

    startLocation: function () {
      OnsiteBridge.stopLocation();
      var generation = OnsiteBridge.geoGeneration;
      if (!navigator.geolocation) {
        OnsiteBridge.send("OnGeoError", "このブラウザーは位置情報に対応していません");
        return;
      }
      try {
        OnsiteBridge.watchId = navigator.geolocation.watchPosition(
          function (position) {
            if (generation !== OnsiteBridge.geoGeneration) return;
            var c = position.coords;
            OnsiteBridge.send("OnGeo", [c.latitude, c.longitude, c.accuracy].join(","));
          },
          function (error) {
            if (generation !== OnsiteBridge.geoGeneration) return;
            OnsiteBridge.send("OnGeoError", error.code + ": " + error.message);
          },
          { enableHighAccuracy: true, maximumAge: 0, timeout: 20000 }
        );
      } catch (error) {
        OnsiteBridge.send("OnGeoError", String(error.message || error));
      }
    },

    cancelPhoto: function () {
      if (OnsiteBridge.photo) OnsiteBridge.photo.finish("", null, false);
    },

    selectPhoto: function () {
      // 二重起動でも DOM や画像 URL を残さない。
      OnsiteBridge.cancelPhoto();
      var input = document.createElement("input");
      input.type = "file";
      input.accept = "image/*";
      input.setAttribute("capture", "environment");
      input.style.display = "none";
      var state = {
        processing: false,
        finished: false,
        timer: null,
        url: null,
        image: null,
        finish: null
      };
      var focus = function () {
        state.timer = setTimeout(function () {
          if (!state.processing && (!input.files || input.files.length === 0)) state.finish("", null, true);
        }, 500);
      };
      state.finish = function (base64, error, notify) {
        if (state.finished) return;
        state.finished = true;
        if (state.timer !== null) clearTimeout(state.timer);
        window.removeEventListener("focus", focus);
        input.onchange = null;
        input.oncancel = null;
        if (state.image) {
          state.image.onload = null;
          state.image.onerror = null;
        }
        if (state.url) URL.revokeObjectURL(state.url);
        if (input.parentNode) input.parentNode.removeChild(input);
        if (OnsiteBridge.photo === state) OnsiteBridge.photo = null;
        if (!notify) return;
        if (error) OnsiteBridge.send("OnPhotoError", error);
        else OnsiteBridge.send("OnPhoto", base64);
      };
      OnsiteBridge.photo = state;
      input.oncancel = function () { state.finish("", null, true); };
      input.onchange = function () {
        if (state.finished) return;
        var file = input.files && input.files[0];
        if (!file) { state.finish("", null, true); return; }
        state.processing = true;
        try {
          state.url = URL.createObjectURL(file);
          var image = new Image();
          state.image = image;
          image.onerror = function () { state.finish("", "画像を読み込めませんでした", true); };
          image.onload = function () {
            if (state.finished) return;
            try {
              if (!image.naturalWidth || !image.naturalHeight) throw new Error("画像のサイズが不正です");
              var scale = Math.min(1, 1600 / Math.max(image.naturalWidth, image.naturalHeight));
              var canvas = document.createElement("canvas");
              canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
              canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
              var context = canvas.getContext("2d");
              if (!context) throw new Error("画像を変換できませんでした");
              // JPEG に透明色がないため、透明部分を白で埋める。
              context.fillStyle = "#fff";
              context.fillRect(0, 0, canvas.width, canvas.height);
              context.drawImage(image, 0, 0, canvas.width, canvas.height);
              var data = canvas.toDataURL("image/jpeg", 0.82);
              if (data.indexOf("data:image/jpeg;base64,") !== 0) throw new Error("JPEG に変換できませんでした");
              state.finish(data.substring(data.indexOf(",") + 1), null, true);
            } catch (error) {
              state.finish("", String(error.message || error), true);
            }
          };
          image.src = state.url;
        } catch (error) {
          state.finish("", String(error.message || error), true);
        }
      };
      window.addEventListener("focus", focus);
      try {
        document.body.appendChild(input);
        // UI のクリックから直接呼び、ブラウザーのユーザー操作を保つ。
        input.click();
      } catch (error) {
        state.finish("", String(error.message || error), true);
      }
    }
  },

  Onsite_StartLocation__deps: ["$OnsiteBridge"],
  Onsite_StartLocation: function () { OnsiteBridge.startLocation(); },
  Onsite_StopLocation__deps: ["$OnsiteBridge"],
  Onsite_StopLocation: function () { OnsiteBridge.stopLocation(); },
  Onsite_SelectPhoto__deps: ["$OnsiteBridge"],
  Onsite_SelectPhoto: function () { OnsiteBridge.selectPhoto(); },
  Onsite_CancelPhoto__deps: ["$OnsiteBridge"],
  Onsite_CancelPhoto: function () { OnsiteBridge.cancelPhoto(); },

  Onsite_Vibrate: function (ms) {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {}
  },

  Onsite_VibratePattern: function (ptr, len) {
    try {
      if (!navigator.vibrate || !ptr || len < 1 || len > 32) return;
      var start = ptr >>> 2;
      if ((ptr & 3) !== 0 || start + len > HEAP32.length) return;
      var pattern = [];
      for (var i = 0; i < len; i++) pattern.push(Math.max(0, Math.min(1000, HEAP32[start + i])));
      navigator.vibrate(pattern);
    } catch (e) {}
  },

  // 日本語入力（IME）を確実に使うため、文字の入力はブラウザーの入力欄で行う
  Onsite_EditText: function (titlePtr, valuePtr, max, multiline) {
    var title = UTF8ToString(titlePtr), value = UTF8ToString(valuePtr);
    var old = document.getElementById('onsite-edit'); if (old) old.remove();
    var wrap = document.createElement('div');
    wrap.id = 'onsite-edit';
    wrap.style.cssText = 'position:fixed;inset:0;z-index:50;background:rgba(2,6,10,.82);display:flex;align-items:center;justify-content:center;padding:16px;font-family:system-ui,sans-serif';
    var box = document.createElement('div');
    box.style.cssText = 'width:min(560px,100%);border-top:1px solid rgba(127,227,255,.8);padding:16px 0;color:#eafbff';
    var label = document.createElement('div'); label.textContent = title;
    label.style.cssText = 'font-size:13px;letter-spacing:.12em;color:#7fa3ab;margin-bottom:10px';
    var input = document.createElement(multiline ? 'textarea' : 'input');
    input.value = value; input.maxLength = max;
    input.style.cssText = 'width:100%;box-sizing:border-box;font-size:17px;padding:12px;background:rgba(127,227,255,.07);color:#eafbff;border:1px solid rgba(160,230,255,.35);outline:none;' + (multiline ? 'height:180px;resize:none' : '');
    var row = document.createElement('div'); row.style.cssText = 'display:flex;justify-content:flex-end;gap:12px;margin-top:14px';
    function btn(text, primary, fn) {
      var b = document.createElement('button'); b.textContent = text;
      b.style.cssText = 'font-size:15px;padding:10px 18px;border:0;cursor:pointer;color:' + (primary ? '#05080a;background:#7fe3ff' : '#eafbff;background:transparent');
      b.onclick = fn; return b;
    }
    row.appendChild(btn('やめる', false, function () { wrap.remove(); SendMessage('OnsiteBridge', 'OnTextEdited', value); }));
    row.appendChild(btn('決定', true, function () { var v = input.value; wrap.remove(); SendMessage('OnsiteBridge', 'OnTextEdited', v); }));
    box.appendChild(label); box.appendChild(input); box.appendChild(row); wrap.appendChild(box);
    document.body.appendChild(wrap);
    setTimeout(function () { input.focus(); }, 30);
  },

  // 効果音：Unity から波形を受け取り、WebAudio で鳴らす（スレッド版の制限を避ける）
  $OnsiteAudio: {
    ctx: null, buffers: {}, voices: {}, next: 1,
    context: function () {
      if (!OnsiteAudio.ctx) {
        var C = window.AudioContext || window.webkitAudioContext;
        if (!C) return null;
        OnsiteAudio.ctx = new C();
        // 録画用に、効果音を音声トラックとしても取り出せるようにする
        try { OnsiteAudio.rec = OnsiteAudio.ctx.createMediaStreamDestination(); window.__onsiteAudioStream = OnsiteAudio.rec.stream; } catch (e) {}
        var resume = function () { if (OnsiteAudio.ctx.state !== 'running') OnsiteAudio.ctx.resume(); };
        ['pointerdown', 'touchend', 'keydown'].forEach(function (t) { document.addEventListener(t, resume, true); });
      }
      return OnsiteAudio.ctx;
    }
  },
  Onsite_AudioRegister__deps: ['$OnsiteAudio'],
  Onsite_AudioRegister: function (idPtr, dataPtr, length, rate) {
    try {
      var ctx = OnsiteAudio.context(); if (!ctx) return;
      // 共有メモリから、共有でない配列へ写す（slice は新しい領域を作る）
      var copy = HEAPF32.slice(dataPtr >> 2, (dataPtr >> 2) + length);
      var buffer = ctx.createBuffer(1, length, rate);
      buffer.getChannelData(0).set(copy);
      OnsiteAudio.buffers[UTF8ToString(idPtr)] = buffer;
    } catch (e) { console.warn('onsite audio', e); }
  },
  Onsite_AudioPlay__deps: ['$OnsiteAudio'],
  Onsite_AudioPlay: function (idPtr, volume, loop) {
    try {
      var ctx = OnsiteAudio.context(); var buffer = OnsiteAudio.buffers[UTF8ToString(idPtr)];
      if (!ctx || !buffer || ctx.state !== 'running') return -1;
      var src = ctx.createBufferSource(); src.buffer = buffer; src.loop = !!loop;
      var gain = ctx.createGain(); gain.gain.value = volume;
      src.connect(gain); gain.connect(ctx.destination); if (OnsiteAudio.rec) gain.connect(OnsiteAudio.rec); src.start();
      var handle = OnsiteAudio.next++;
      OnsiteAudio.voices[handle] = { src: src, gain: gain };
      src.onended = function () { delete OnsiteAudio.voices[handle]; };
      return handle;
    } catch (e) { return -1; }
  },
  Onsite_AudioVolume__deps: ['$OnsiteAudio'],
  Onsite_AudioVolume: function (handle, volume) {
    var v = OnsiteAudio.voices[handle]; if (v) v.gain.gain.value = volume;
  },
  Onsite_AudioStop__deps: ['$OnsiteAudio'],
  Onsite_AudioStop: function (handle) {
    var v = OnsiteAudio.voices[handle]; if (v) { try { v.src.stop(); } catch (e) {} delete OnsiteAudio.voices[handle]; }
  },
  Onsite_AudioStopAll__deps: ['$OnsiteAudio'],
  Onsite_AudioStopAll: function () {
    for (var h in OnsiteAudio.voices) { try { OnsiteAudio.voices[h].src.stop(); } catch (e) {} }
    OnsiteAudio.voices = {};
  }
});
