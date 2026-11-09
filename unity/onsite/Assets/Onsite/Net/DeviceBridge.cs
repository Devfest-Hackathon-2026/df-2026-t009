using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Onsite.Net
{
    public enum HapticKind { Tap, Select, Arrival, Unlock, Complete, Error }

    [DisallowMultipleComponent]
    public sealed class DeviceBridge : MonoBehaviour
    {
        public event Action<double, double, float> OnLocation;
        public event Action<byte[]> OnPhotoReady;
        public event Action<string> LocationError;
        public event Action<string> PhotoError;

        static DeviceBridge active;
        static readonly int[][] hapticPatterns =
        {
            new[] { 8 }, new[] { 14 }, new[] { 30, 60, 30 },
            new[] { 20, 40, 20, 40, 80 }, new[] { 40, 60, 40, 60, 120 }, new[] { 60, 40, 60 }
        };
        readonly float[] lastHaptics = { -1f, -1f, -1f, -1f, -1f, -1f };

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetString("onsite.haptics", "1") != "0";
            set
            {
                PlayerPrefs.SetString("onsite.haptics", value ? "1" : "0");
                PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!value) Onsite_Vibrate(0);
#endif
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void Onsite_StartLocation();
        [DllImport("__Internal")] private static extern void Onsite_StopLocation();
        [DllImport("__Internal")] private static extern void Onsite_SelectPhoto();
        [DllImport("__Internal")] private static extern void Onsite_CancelPhoto();
        [DllImport("__Internal")] private static extern void Onsite_Vibrate(int ms);
        [DllImport("__Internal")] private static extern void Onsite_VibratePattern(int[] pattern, int len);
        [DllImport("__Internal")] private static extern void Onsite_EditText(string title, string value, int max, bool multiline);
#else
        private Coroutine locationRoutine;
        private bool locationRequested;
#endif

        private void Awake()
        {
            // SendMessage の宛先を、シーンでの名前変更から守る。
            gameObject.name = "OnsiteBridge";
        }

        private void OnEnable() => active = this;

        public static void Feedback(HapticKind kind)
        {
            if (active != null) active.Haptic(kind);
        }

        public void Haptic(HapticKind kind)
        {
            var index = (int)kind;
            if (!isActiveAndEnabled || !HapticsEnabled || index < 0 || index >= hapticPatterns.Length) return;
            var now = Time.realtimeSinceStartup;
            if (now - lastHaptics[index] < .06f) return;
            lastHaptics[index] = now;
#if UNITY_WEBGL && !UNITY_EDITOR
            var pattern = hapticPatterns[index];
            Onsite_VibratePattern(pattern, pattern.Length);
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android の標準 API は長さを選べない。
            if (kind >= HapticKind.Select) Handheld.Vibrate();
#endif
        }

        public void StartLocation()
        {
            if (!isActiveAndEnabled) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            Onsite_StartLocation();
#else
            StopLocation();
            locationRequested = true;
            locationRoutine = StartCoroutine(WatchLocation());
#endif
        }

        public void StopLocation()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Onsite_StopLocation();
#else
            locationRequested = false;
            if (locationRoutine != null) StopCoroutine(locationRoutine);
            locationRoutine = null;
            if (Input.location.status != LocationServiceStatus.Stopped) Input.location.Stop();
#endif
        }

        /// <summary>端末を短く震わせる（範囲に入ったときの合図）。対応しない端末では何もしない。</summary>
        public void Vibrate(int ms)
        {
            if (!isActiveAndEnabled || !HapticsEnabled || ms <= 0) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            Onsite_Vibrate(ms);
#elif UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        /// <summary>ブラウザーの入力欄で文字を入れてもらう（日本語の変換を確実に使うため）。</summary>
        public event Action<string> TextEdited;
        public static bool UsesHtmlText =>
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif
        public void EditText(string title, string value, int max, bool multiline)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Onsite_EditText(title ?? "", value ?? "", max, multiline);
#endif
        }
        public void OnTextEdited(string text) => TextEdited?.Invoke(text);

        public void TakePhoto()
        {
            if (!isActiveAndEnabled) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            Onsite_SelectPhoto();
#else
            // TODO: Android の写真選択はネイティブ実装を追加するまで null を返す。
            OnPhotoReady?.Invoke(null);
#endif
        }

        private void OnDisable()
        {
            if (active == this) active = null;
            StopLocation();
#if UNITY_WEBGL && !UNITY_EDITOR
            Onsite_CancelPhoto();
#endif
        }

        // JavaScript から呼ぶため、メソッド名と引数の形を固定する。
        [UnityEngine.Scripting.Preserve]
        public void OnGeo(string coordinates)
        {
            var parts = coordinates?.Split(',');
            if (parts == null || parts.Length != 3 ||
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var accuracy) ||
                double.IsNaN(lat) || double.IsNaN(lng) || float.IsNaN(accuracy) ||
                lat < -90 || lat > 90 || lng < -180 || lng > 180 ||
                accuracy < 0 || float.IsInfinity(accuracy))
            {
                OnGeoError("位置情報の形式が不正です");
                return;
            }
            if (isActiveAndEnabled) OnLocation?.Invoke(lat, lng, accuracy);
        }

        [UnityEngine.Scripting.Preserve]
        public void OnGeoError(string message)
        {
            if (isActiveAndEnabled) Rest.Fail(LocationError, message);
        }

        [UnityEngine.Scripting.Preserve]
        public void OnPhoto(string base64)
        {
            if (!isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(base64))
            {
                OnPhotoReady?.Invoke(null);
                return;
            }
            byte[] jpeg = null;
            string problem = null;
            try
            {
                jpeg = Convert.FromBase64String(base64);
                if (jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8)
                    throw new FormatException("写真が JPEG ではありません");
            }
            catch (FormatException e) { problem = e.Message; }
            if (problem != null) OnPhotoError(problem);
            else OnPhotoReady?.Invoke(jpeg);
        }

        [UnityEngine.Scripting.Preserve]
        public void OnPhotoError(string message)
        {
            if (!isActiveAndEnabled) return;
            OnPhotoReady?.Invoke(null);
            Rest.Fail(PhotoError, message);
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private IEnumerator WatchLocation()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                var complete = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => complete = true;
                callbacks.PermissionDenied += _ => complete = true;
                Permission.RequestUserPermission(Permission.FineLocation, callbacks);
                while (!complete && locationRequested) yield return null;
                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    OnGeoError("位置情報の権限がありません");
                    yield break;
                }
            }
#endif
            if (!Input.location.isEnabledByUser)
            {
                OnGeoError("端末の位置情報を有効にしてください");
                yield break;
            }
            Input.location.Start(1f, 1f);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Input.location.status == LocationServiceStatus.Initializing &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Stop();
                OnGeoError("位置情報を取得できませんでした");
                yield break;
            }
            var timestamp = double.NaN;
            while (locationRequested)
            {
                if (Input.location.status == LocationServiceStatus.Failed)
                {
                    Input.location.Stop();
                    OnGeoError("位置情報の取得が中断されました");
                    yield break;
                }
                if (Input.location.status == LocationServiceStatus.Running)
                {
                    var location = Input.location.lastData;
                    if (location.timestamp != timestamp)
                    {
                        timestamp = location.timestamp;
                        OnLocation?.Invoke(location.latitude, location.longitude, location.horizontalAccuracy);
                    }
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }
#endif
    }
}