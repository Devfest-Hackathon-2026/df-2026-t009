using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute
    { public string fileName; public string menuName; }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponentAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    public class Object { public static void Destroy(Object value) { } public static void DontDestroyOnLoad(Object value) { } }
    public class ScriptableObject : Object { }
    public sealed class GameObject : Object { public string name; }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new GameObject();
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled;
        protected Coroutine StartCoroutine(IEnumerator value) => new Coroutine();
        protected void StopCoroutine(Coroutine value) { }
    }
    public sealed class Coroutine { }
    public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
    public static class Time { public static float realtimeSinceStartup => 0; }
    public enum LocationServiceStatus { Stopped, Initializing, Running, Failed }
    public struct LocationInfo { public double timestamp; public float latitude, longitude, horizontalAccuracy; }
    public sealed class LocationService
    {
        public bool isEnabledByUser;
        public LocationServiceStatus status;
        public LocationInfo lastData;
        public void Start(float accuracy, float distance) { }
        public void Stop() { status = LocationServiceStatus.Stopped; }
    }
    public static class Input { public static LocationService location = new LocationService(); }
    public static class Debug { public static void LogError(object value) { throw new Exception("Unexpected log: " + value); } }
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public static string GetString(string key, string fallback) => Data.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) { Data[key] = value; }
        public static void DeleteKey(string key) { Data.Remove(key); }
        public static void Save() { }
    }
    public sealed class WWWForm
    {
        public readonly Dictionary<string, string> fields = new Dictionary<string, string>();
        public void AddField(string key, string value) { fields[key] = value; }
    }
}
namespace UnityEngine.Scripting
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class PreserveAttribute : Attribute { }
}
namespace UnityEngine.Networking
{
    public sealed class UploadHandlerRaw
    {
        public byte[] data;
        public UploadHandlerRaw(byte[] data) { this.data = data; }
    }
    public sealed class DownloadHandlerBuffer { public string text; }
    public sealed class UnityWebRequestAsyncOperation { }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public string url, method, error;
        public int timeout;
        public long responseCode;
        public Result result;
        public UploadHandlerRaw uploadHandler;
        public DownloadHandlerBuffer downloadHandler;
        public UnityEngine.WWWForm form;
        public Dictionary<string, string> headers = new Dictionary<string, string>();
        public static readonly Queue<Action<UnityWebRequest>> Responses = new Queue<Action<UnityWebRequest>>();
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public static UnityWebRequest Post(string url, UnityEngine.WWWForm form) =>
            new UnityWebRequest(url, "POST") { form = form, downloadHandler = new DownloadHandlerBuffer() };
        public void SetRequestHeader(string name, string value) { headers[name] = value; }
        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            if (Responses.Count == 0) throw new Exception("Unexpected request: " + url);
            Responses.Dequeue()(this);
            return new UnityWebRequestAsyncOperation();
        }
        public void Dispose() { }
    }
}