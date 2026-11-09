#if UNITY_EDITOR
using System.IO;
using System.Linq;
using CesiumForUnity;
using Onsite.Map;
using Onsite.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Onsite.EditorTools
{
    /// <summary>メインのシーンを、コードから組み立てる（手作業の設定を残さないため）。</summary>
    public static class OnsiteSceneBuilder
    {
        const string Settings = "Assets/Onsite/Settings";
        const string Resources = "Assets/Onsite/Resources";

        [MenuItem("Onsite/0. web/.env.local から鍵を読み込む")]
        public static void ImportSecrets()
        {
            Directory.CreateDirectory(Resources);
            var env = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../web/.env.local"));
            if (!File.Exists(env)) { Debug.LogError($"[onsite] {env} がありません"); return; }
            var kv = File.ReadAllLines(env).Where(l => l.Contains('=') && !l.TrimStart().StartsWith("#"))
                .Select(l => l.Split(new[] { '=' }, 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            string Get(string k) => kv.TryGetValue(k, out var v) ? v : "";

            var s = Load<OnsiteSecrets>($"{Resources}/OnsiteSecrets.asset");
            s.firebaseApiKey = Get("VITE_FIREBASE_API_KEY");
            s.firebaseProjectId = Get("VITE_FIREBASE_PROJECT_ID");
            s.firebaseStorageBucket = Get("VITE_FIREBASE_STORAGE_BUCKET");
            s.mapsApiKey = Get("VITE_GOOGLE_MAPS_API_KEY");
            EditorUtility.SetDirty(s);

            var f = Load<FirebaseConfig>($"{Resources}/FirebaseConfig.asset");
            f.apiKey = s.firebaseApiKey;
            f.projectId = s.firebaseProjectId;
            f.storageBucket = s.firebaseStorageBucket;
            EditorUtility.SetDirty(f);
            AssetDatabase.SaveAssets();
            Debug.Log("[onsite] 鍵を読み込みました（Git には入りません）");
        }

        public static void Build(string scenePath)
        {
            ImportSecrets();
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 測量マップの画面効果（URP の Full Screen Pass）
            InstallHoloPass();

            // カメラ
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.04f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 30000f;
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var orbit = camGo.AddComponent<OrbitCamera>();

            // 光（金の像を照らす）
            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.82f);
            sun.intensity = 1.6f;
            sun.transform.rotation = Quaternion.Euler(38, 140, 0);
            var rim = new GameObject("Rim Light").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.5f, 0.89f, 1f);
            rim.intensity = 1.1f;
            rim.transform.rotation = Quaternion.Euler(20, -40, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.18f);

            // 後処理（ブルーム・トーンマップ・周辺減光・わずかな粒子）
            var vol = new GameObject("Post").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = PostProfile();

            // 地図
            var globe = new GameObject("Globe");
            var geo = globe.AddComponent<CesiumGeoreference>();
            geo.SetOriginLongitudeLatitudeHeight(139.7222, 35.7088, 40);
            var tilesGo = new GameObject("Google 3D Tiles");
            tilesGo.transform.SetParent(globe.transform, false);
            var tiles = tilesGo.AddComponent<Cesium3DTileset>();
            tiles.tilesetSource = CesiumDataSource.FromUrl;
            tiles.url = ""; // 実行時に鍵から作る（シーンに鍵を残さない）
            tiles.showCreditsOnScreen = true;
            var world = globe.AddComponent<MapWorld>();
            world.georeference = geo;
            world.tileset = tiles;

            var markers = new GameObject("Markers").AddComponent<MarkerField>();
            markers.world = world;
            var paths = new GameObject("Paths").AddComponent<PathLayer>();
            paths.world = world;
            var avatar = new GameObject("Avatar").AddComponent<AvatarRig>();
            orbit.target = avatar.transform;

            // 端末・通信
            var bridge = new GameObject("OnsiteBridge");
            bridge.AddComponent<DeviceBridge>();
            var apiGo = new GameObject("OnsiteApi");
            var apiType = System.Type.GetType("Onsite.Net.OnsiteApi, Assembly-CSharp");
            if (apiType != null)
            {
                var api = apiGo.AddComponent(apiType);
                // Firebase の設定（鍵は Resources の Git 管理外アセット）をつなぐ
                var so = new SerializedObject(api);
                so.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FirebaseConfig>($"{Resources}/FirebaseConfig.asset");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // UI
            var uiGo = new GameObject("HUD");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = PanelSettingsAsset();
            var appType = System.Type.GetType("Onsite.App.OnsiteApp, Assembly-CSharp");
            if (appType != null) uiGo.AddComponent(appType);

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            Debug.Log($"[onsite] シーンを作りました: {scenePath}");
        }

        static void InstallHoloPass()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"{Settings}/OnsiteRenderer.asset");
            if (data == null) { OnsiteSetup.Setup(); data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"{Settings}/OnsiteRenderer.asset"); }
            var matPath = $"{Settings}/HoloMap.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Onsite/HoloMap")); AssetDatabase.CreateAsset(mat, matPath); }
            // 見た目の調整値（ここを変えれば再ビルドで反映される）
            mat.SetFloat("_Mono", 0.42f);
            mat.SetFloat("_EdgeStrength", 0.08f);
            mat.SetFloat("_ContourStep", 3f);
            mat.SetFloat("_ContourWidth", 0.8f);
            mat.SetFloat("_GridSize", 40f);
            mat.SetFloat("_FogStart", 900f);
            mat.SetFloat("_FogEnd", 3200f);
            mat.SetFloat("_Scan", 0.08f);
            EditorUtility.SetDirty(mat);
            if (data.rendererFeatures.OfType<FullScreenPassRendererFeature>().Any()) { AssetDatabase.SaveAssets(); return; }
            var f = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            f.name = "Holo Map";
            f.passMaterial = mat;
            f.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            f.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color;
            f.fetchColorBuffer = true;
            AssetDatabase.AddObjectToAsset(f, data);
            data.rendererFeatures.Add(f);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        static VolumeProfile PostProfile()
        {
            var path = $"{Settings}/OnsitePost.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(1.4f); bloom.threshold.Override(0.85f); bloom.scatter.Override(0.72f);
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.38f); vig.smoothness.Override(0.5f);
            var ca = p.Add<ChromaticAberration>(true); ca.intensity.Override(0.08f);
            var grain = p.Add<FilmGrain>(true); grain.intensity.Override(0.12f);
            var color = p.Add<ColorAdjustments>(true); color.contrast.Override(12f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static PanelSettings PanelSettingsAsset()
        {
            var path = $"{Settings}/HUDPanel.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps != null) return ps;
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(720, 1280);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            AssetDatabase.CreateAsset(ps, path);
            return ps;
        }

        static T Load<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
#endif
