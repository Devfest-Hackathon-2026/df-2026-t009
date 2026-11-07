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
  Onsite_CancelPhoto: function () { OnsiteBridge.cancelPhoto(); }
});