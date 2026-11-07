using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Onsite.Net
{
    public sealed class FirebaseAuthRest
    {
        private const string UidKey = "onsite.uid";
        private const string RefreshKey = "onsite.refresh";
        private readonly FirebaseConfig config;
        private string refreshToken;
        private string idToken;
        private DateTime expiresAt;
        private bool signingIn;
        private int generation;

        public string Uid { get; private set; }
        public string LastError { get; private set; }
        public bool IsSignedIn => !string.IsNullOrEmpty(idToken) && DateTime.UtcNow < expiresAt;
        public event Action<string> Error;
        public event Action SignedOut;

        public FirebaseAuthRest(FirebaseConfig config)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Uid = PlayerPrefs.GetString(UidKey, "");
            refreshToken = PlayerPrefs.GetString(RefreshKey, "");
        }

        public IEnumerator EnsureSignedIn() => EnsureSignedIn(generation);

        private IEnumerator EnsureSignedIn(int expectedGeneration)
        {
            if (expectedGeneration != generation) yield break;
            if (IsSignedIn && expiresAt - DateTime.UtcNow > TimeSpan.FromMinutes(5)) yield break;
            if (signingIn)
            {
                var waitingGeneration = generation;
                while (signingIn && waitingGeneration == generation) yield return null;
                yield break;
            }
            if (string.IsNullOrWhiteSpace(config.apiKey))
            {
                Report("FirebaseConfig の API キーを設定してください");
                yield break;
            }

            var current = generation;
            signingIn = true;
            LastError = null;
            try
            {
                // 同時呼び出しでも更新トークンを一度だけ交換する。
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var form = new WWWForm();
                    form.AddField("grant_type", "refresh_token");
                    form.AddField("refresh_token", refreshToken);
                    object response = null;
                    string problem = null;
                    using (var request = UnityWebRequest.Post(
                        "https://securetoken.googleapis.com/v1/token?key=" + Uri.EscapeDataString(config.apiKey), form))
                    {
                        request.timeout = 30;
                        yield return Rest.Send(request, value => response = value, value => problem = value);
                    }
                    if (current != generation) yield break;
                    if (problem == null)
                    {
                        Accept(response, true);
                        yield break;
                    }
                    if (problem != "INVALID_REFRESH_TOKEN" && problem != "TOKEN_EXPIRED" && problem != "USER_NOT_FOUND")
                    {
                        Report(problem);
                        yield break;
                    }
                    // 無効になった匿名アカウントだけを作り直し、通信障害では履歴を捨てない。
                    Forget();
                    SignedOut?.Invoke();
                }
                if (current != generation) yield break;

                object signup = null;
                string signupError = null;
                using (var request = Rest.JsonRequest(
                    "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + Uri.EscapeDataString(config.apiKey),
                    "POST", new Dictionary<string, object> { { "returnSecureToken", true } }))
                {
                    yield return Rest.Send(request, value => signup = value, value => signupError = value);
                }
                if (current != generation) yield break;
                if (signupError != null) Report(signupError);
                else Accept(signup, false);
            }
            finally
            {
                // サインアウト前に始めた処理は、新しいログイン状態に触れない。
                if (current == generation) signingIn = false;
            }
        }

        public IEnumerator GetIdToken(Action<string> done) => GetIdToken(done, generation);

        private IEnumerator GetIdToken(Action<string> done, int current)
        {
            yield return EnsureSignedIn(current);
            done?.Invoke(current == generation && LastError == null && IsSignedIn ? idToken : null);
        }

        public void SignOutAndForget()
        {
            generation++;
            signingIn = false;
            Forget();
            LastError = null;
            SignedOut?.Invoke();
        }

        private void Accept(object response, bool refreshing)
        {
            string problem = null;
            try
            {
                var data = Values.Map(response);
                var nextId = Values.Text(data, refreshing ? "id_token" : "idToken");
                var nextRefresh = Values.Text(data, refreshing ? "refresh_token" : "refreshToken");
                var nextUid = Values.Text(data, refreshing ? "user_id" : "localId");
                var seconds = Values.Number(data, refreshing ? "expires_in" : "expiresIn") ?? 0;
                if (string.IsNullOrEmpty(nextId) || string.IsNullOrEmpty(nextRefresh) ||
                    string.IsNullOrEmpty(nextUid) || seconds <= 300 || seconds > 86400)
                    throw new FormatException("認証サーバーの応答が不正です");
                idToken = nextId;
                refreshToken = nextRefresh;
                Uid = nextUid;
                expiresAt = DateTime.UtcNow.AddSeconds(seconds);
                PlayerPrefs.SetString(UidKey, Uid);
                PlayerPrefs.SetString(RefreshKey, refreshToken);
                PlayerPrefs.Save();
                LastError = null;
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                problem = e.Message;
            }
            if (problem != null) Report(problem);
        }

        private void Forget()
        {
            Uid = "";
            refreshToken = "";
            idToken = null;
            expiresAt = DateTime.MinValue;
            PlayerPrefs.DeleteKey(UidKey);
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.Save();
        }

        private void Report(string message)
        {
            LastError = message;
            Rest.Fail(Error, message);
        }
    }
}