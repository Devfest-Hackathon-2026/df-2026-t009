using UnityEngine;

namespace Onsite.Net
{
    [CreateAssetMenu(fileName = "FirebaseConfig", menuName = "Onsite/Firebase Config")]
    public sealed class FirebaseConfig : ScriptableObject
    {
        public string apiKey;
        public string projectId;
        public string region = "asia-northeast1";
        public string storageBucket;
    }
}