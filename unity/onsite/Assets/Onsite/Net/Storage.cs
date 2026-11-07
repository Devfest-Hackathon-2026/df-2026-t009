using System;
using System.Collections;
using UnityEngine.Networking;

namespace Onsite.Net
{
    public sealed class Storage
    {
        private readonly FirebaseConfig config;
        private readonly FirebaseAuthRest auth;

        public Storage(FirebaseConfig config, FirebaseAuthRest auth)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public IEnumerator UploadJpeg(byte[] jpeg, Action<string> done, Action<string> error = null)
        {
            // Storage のルールは 3MiB 未満。失敗が確実な画像は送らない。
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8 ||
                jpeg[jpeg.Length - 2] != 0xff || jpeg[jpeg.Length - 1] != 0xd9)
            {
                Rest.Fail(error, "JPEG 画像を指定してください");
                yield break;
            }
            if (jpeg.Length >= 3 * 1024 * 1024)
            {
                Rest.Fail(error, "写真は 3MiB 未満にしてください");
                yield break;
            }
            if (string.IsNullOrWhiteSpace(config.storageBucket))
            {
                Rest.Fail(error, "FirebaseConfig の Storage バケットを設定してください");
                yield break;
            }
            string token = null;
            yield return auth.GetIdToken(value => token = value);
            if (token == null)
            {
                Rest.Fail(error, auth.LastError ?? "ログインが必要です");
                yield break;
            }
            var path = "uploads/" + auth.Uid + "/" + Guid.NewGuid().ToString("N") + ".jpg";
            var url = "https://firebasestorage.googleapis.com/v0/b/" +
                Uri.EscapeDataString(config.storageBucket) + "/o?uploadType=media&name=" + Uri.EscapeDataString(path);
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.timeout = 60;
                request.uploadHandler = new UploadHandlerRaw(jpeg);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.SetRequestHeader("Content-Type", "image/jpeg");
                string problem = null;
                yield return Rest.Send(request, _ => { }, value => problem = value);
                if (problem != null) Rest.Fail(error, problem);
                else done?.Invoke(path);
            }
        }
    }
}