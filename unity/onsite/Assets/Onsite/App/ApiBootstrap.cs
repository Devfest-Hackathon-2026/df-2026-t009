using System;
using Onsite.Net;
using UnityEngine;

namespace Onsite.App
{
    internal static class ApiBootstrap
    {
        [Serializable]
        sealed class Configuration { public FirebaseConfig config; }

        public static OnsiteApi Ensure()
        {
            if (OnsiteApi.Instance != null) return OnsiteApi.Instance;
            var config = Resources.Load<FirebaseConfig>("FirebaseConfig");
            if (config == null) return null;
            // シーンビルダーの未割当を、公開された実行時シリアライズで補う。
            // 参照の InstanceID は同じ実行中にだけ受け渡し、保存やログには出さない。
            var go = new GameObject("OnsiteApi Runtime");
            go.SetActive(false);
            var api = go.AddComponent<OnsiteApi>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Configuration { config = config }), api);
            go.SetActive(true);
            return OnsiteApi.Instance;
        }
    }
}
