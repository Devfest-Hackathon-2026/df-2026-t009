using UnityEngine;

namespace Onsite.Map
{
    /// <summary>
    /// 地図と Firebase のキー。Resources/OnsiteSecrets.asset に置き、Git には入れない（.gitignore 済み）。
    /// エディタのメニュー「Onsite/0. .env.local から鍵を読み込む」で web/.env.local から作る。
    /// </summary>
    public class OnsiteSecrets : ScriptableObject
    {
        public string firebaseApiKey;
        public string firebaseProjectId = "kokodake-t009";
        public string firebaseStorageBucket;
        public string mapsApiKey;

        static OnsiteSecrets _cached;
        public static OnsiteSecrets Load()
        {
            if (_cached == null) _cached = Resources.Load<OnsiteSecrets>("OnsiteSecrets");
            return _cached;
        }

        public string GoogleTilesUrl => $"https://tile.googleapis.com/v1/3dtiles/root.json?key={mapsApiKey}";
    }
}
