using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace UnityEngine
{
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public static string GetString(string key, string fallback = "") => Data.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) => Data[key] = value;
        public static void DeleteKey(string key) => Data.Remove(key);
        public static void Save() { }
    }
    public static class JsonUtility
    {
        public static string ToJson(object value) => new JavaScriptSerializer().Serialize(value);
        public static T FromJson<T>(string json)
        {
            try { return new JavaScriptSerializer().Deserialize<T>(json); }
            catch (Exception problem) { throw new ArgumentException("Invalid test JSON", problem); }
        }
    }
}
