using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Onsite.Net
{
    internal static class Rest
    {
        internal static UnityWebRequest JsonRequest(string url, string method, object body)
        {
            var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(Json.Stringify(body)));
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }
            request.timeout = 30;
            return request;
        }

        internal static IEnumerator Send(UnityWebRequest request, Action<object> done, Action<string> error)
        {
            yield return request.SendWebRequest();
            object parsed = null;
            string problem = null;
            try
            {
                var text = request.downloadHandler.text;
                if (!string.IsNullOrWhiteSpace(text)) parsed = Json.Parse(text);
                if (parsed is Dictionary<string, object> root && root.TryGetValue("error", out var failure))
                {
                    if (failure is Dictionary<string, object> details)
                        problem = Values.Text(details, "message") ?? Values.Text(details, "status");
                    else problem = failure as string;
                    problem = problem ?? "サーバーエラー";
                }
                if (problem == null && request.result != UnityWebRequest.Result.Success)
                    problem = request.error ?? ("HTTP " + request.responseCode);
                if (problem == null && parsed == null) problem = "サーバーの応答が空です";
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                problem = request.result == UnityWebRequest.Result.Success
                    ? e.Message : request.error ?? ("HTTP " + request.responseCode);
            }
            if (problem != null) Fail(error, problem);
            else done?.Invoke(parsed);
        }

        internal static void Fail(Action<string> error, string message)
        {
            if (error != null) error(message);
            else Debug.LogError(message);
        }

        internal static string Segment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("/") || value == "." || value == "..")
                throw new ArgumentException("パスの要素が不正です", nameof(value));
            return Uri.EscapeDataString(value);
        }
    }
}