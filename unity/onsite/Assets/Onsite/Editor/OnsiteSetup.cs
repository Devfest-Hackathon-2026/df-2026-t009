#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Onsite.EditorTools
{
    /// <summary>
    /// プロジェクトの設定とビルドを、コマンドラインから再現できるようにまとめたもの。
    ///   Unity.exe -batchmode -projectPath unity/onsite -executeMethod Onsite.EditorTools.OnsiteSetup.Setup -quit
    ///   Unity.exe -batchmode -projectPath unity/onsite -executeMethod Onsite.EditorTools.OnsiteSetup.BuildWebGL -quit
    /// </summary>
    public static class OnsiteSetup
    {
        const string SettingsDir = "Assets/Onsite/Settings";
        const string ScenePath = "Assets/Onsite/Scenes/Main.unity";

        [MenuItem("Onsite/1. プロジェクトを設定")]
        public static void Setup()
        {
            Directory.CreateDirectory(SettingsDir);

            // URP（ブルームなどの後処理を使うため）
            var rendererData = LoadOrCreate<UniversalRendererData>($"{SettingsDir}/OnsiteRenderer.asset", () => ScriptableObject.CreateInstance<UniversalRendererData>());
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{SettingsDir}/OnsiteURP.asset");
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, $"{SettingsDir}/OnsiteURP.asset");
            }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 2;
            urp.supportsCameraDepthTexture = true;
            urp.supportsCameraOpaqueTexture = true;
            urp.renderScale = 1f;
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }

            // プレイヤー設定
            PlayerSettings.companyName = "onsite";
            PlayerSettings.productName = "onsite";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "app.onsite.field");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.runInBackground = true;

            // Web：Cesium は native のマルチスレッドが必要。Firebase Hosting で COOP/COEP を返す。
            PlayerSettings.WebGL.threadsSupport = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.maximumMemorySize = 4096;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[onsite] プロジェクトの設定が終わりました");
        }

        [MenuItem("Onsite/2. シーンを作る")]
        public static void BuildScene() => OnsiteSceneBuilder.Build(ScenePath);

        [MenuItem("Onsite/3. WebGL をビルド")]
        public static void BuildWebGL()
        {
            EnsureScene();
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "../build/webgl",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Report(r);
        }

        [MenuItem("Onsite/4. Android をビルド")]
        public static void BuildAndroid()
        {
            EnsureScene();
            // Unity に同梱の JDK が無い環境向け：ONSITE_JDK で JDK 17 の場所を渡せる
            var jdk = System.Environment.GetEnvironmentVariable("ONSITE_JDK");
            if (!string.IsNullOrEmpty(jdk)) UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = jdk;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "../build/android/onsite.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Report(r);
        }

        static void EnsureScene()
        {
            Setup();
            if (!File.Exists(ScenePath)) BuildScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void Report(UnityEditor.Build.Reporting.BuildReport r)
        {
            var s = r.summary;
            Debug.Log($"[onsite] build {s.result} {s.totalSize / 1024 / 1024}MB errors={s.totalErrors} at {s.outputPath}");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        static T LoadOrCreate<T>(string path, System.Func<T> make) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = make();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
#endif
