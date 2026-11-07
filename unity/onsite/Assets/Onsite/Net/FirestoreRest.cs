using System;
using System.Collections;
using System.Collections.Generic;

namespace Onsite.Net
{
    public sealed class FirestoreRest
    {
        private readonly FirebaseConfig config;
        private readonly FirebaseAuthRest auth;
        private string Root => "https://firestore.googleapis.com/v1/projects/" +
            Rest.Segment(config.projectId) + "/databases/(default)/documents";

        public FirestoreRest(FirebaseConfig config, FirebaseAuthRest auth)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public IEnumerator ListDrops(int limit, Action<Drop[]> done, Action<string> error = null)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
            return Query("", "drops", "createdAt", true, limit, null, false, done, error);
        }

        public IEnumerator GetProfile(string uid, Action<Profile> done, Action<string> error = null)
        {
            return Fetch(Root + "/profiles/" + Rest.Segment(uid), "GET", null, false, true, response =>
            {
                Profile profile = null;
                string problem = null;
                try
                {
                    profile = response == null ? new Profile() : DecodeModel<Profile>(Values.Map(response));
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    problem = e.Message;
                }
                if (problem != null) Rest.Fail(error, problem);
                else done?.Invoke(profile);
            }, error);
        }

        public IEnumerator ListOwn<T>(string uid, string sub, string orderField, Action<T[]> done,
            Action<string> error = null)
        {
            var valid = sub == "opened" && typeof(T) == typeof(OpenedEntry) && orderField == "openedAt" ||
                sub == "placed" && typeof(T) == typeof(PlacedEntry) && orderField == "createdAt" ||
                sub == "notifications" && typeof(T) == typeof(Notice) && orderField == "createdAt" ||
                sub == "liked" && typeof(T) == typeof(LikedEntry) && orderField == "createdAt" ||
                sub == "events" && typeof(T) == typeof(JoinedEventEntry) && orderField == "joinedAt";
            if (!valid) throw new ArgumentException("記録の型・コレクション・並び順が一致しません");
            return Query("/users/" + Rest.Segment(uid), sub, orderField, true, 200, null, true, done, error, uid);
        }

        public IEnumerator ListComments(string dropId, Action<Comment[]> done, Action<string> error = null) =>
            Query("/drops/" + Rest.Segment(dropId), "comments", "createdAt", false, 100, null, true, done, error);

        public IEnumerator ListEvents(Action<KokoEvent[]> done, Action<string> error = null) =>
            Query("", "events", "createdAt", true, 100, null, false, done, error);

        public IEnumerator ListFootprints(double minLat, double maxLat, Action<Footprint[]> done,
            Action<string> error = null)
        {
            if (double.IsNaN(minLat) || double.IsNaN(maxLat) ||
                minLat < -90 || maxLat > 90 || minLat > maxLat)
                throw new ArgumentOutOfRangeException(nameof(minLat));
            var filters = new object[]
            {
                LatitudeFilter("GREATER_THAN_OR_EQUAL", minLat),
                LatitudeFilter("LESS_THAN_OR_EQUAL", maxLat)
            };
            var where = new Dictionary<string, object>
            {
                { "compositeFilter", new Dictionary<string, object> { { "op", "AND" }, { "filters", filters } } }
            };
            return Query("", "footprints", "lat", false, 800, where, false, done, error);
        }

        private static object LatitudeFilter(string operation, double lat) =>
            new Dictionary<string, object>
            {
                { "fieldFilter", new Dictionary<string, object>
                    {
                        { "field", new Dictionary<string, object> { { "fieldPath", "lat" } } },
                        { "op", operation },
                        { "value", new Dictionary<string, object> { { "doubleValue", lat } } }
                    }
                }
            };

        private IEnumerator Query<T>(string parent, string collection, string orderField, bool descending,
            int limit, object where, bool requiresAuth, Action<T[]> done, Action<string> error, string expectedUid = null)
        {
            var query = new Dictionary<string, object>
            {
                { "from", new object[] { new Dictionary<string, object> { { "collectionId", collection } } } },
                { "orderBy", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "field", new Dictionary<string, object> { { "fieldPath", orderField } } },
                            { "direction", descending ? "DESCENDING" : "ASCENDING" }
                        }
                    }
                },
                { "limit", limit }
            };
            if (where != null) query.Add("where", where);
            yield return Fetch(Root + parent + ":runQuery", "POST",
                new Dictionary<string, object> { { "structuredQuery", query } }, requiresAuth, false, response =>
                {
                    T[] result = null;
                    string problem = null;
                    try
                    {
                        if (!(response is List<object> rows)) throw new FormatException("クエリ応答が不正です");
                        var items = new List<T>();
                        foreach (var row in rows)
                        {
                            var entry = Values.Map(row);
                            // 空のクエリにも readTime だけの行がある。
                            if (entry.TryGetValue("document", out var document))
                                items.Add(DecodeModel<T>(Values.Map(document)));
                        }
                        result = items.ToArray();
                    }
                    catch (Exception e) when (e is FormatException || e is OverflowException)
                    {
                        problem = e.Message;
                    }
                    if (problem != null) Rest.Fail(error, problem);
                    else done?.Invoke(result);
                }, error, expectedUid);
        }

        private IEnumerator Fetch(string url, string method, object body, bool requiresAuth, bool allowMissing,
            Action<object> done, Action<string> error, string expectedUid = null)
        {
            // 待機中にサインアウトした所有者向けクエリは、再ログインを始めない。
            if (expectedUid != null && expectedUid != auth.Uid)
            {
                Rest.Fail(error, "自分の記録だけを取得できます");
                yield break;
            }
            string token = null;
            if (requiresAuth || auth.IsSignedIn || !string.IsNullOrEmpty(auth.Uid))
                yield return auth.GetIdToken(value => token = value);
            if (requiresAuth && token == null)
            {
                Rest.Fail(error, auth.LastError ?? "ログインが必要です");
                yield break;
            }
            using (var request = Rest.JsonRequest(url, method, body))
            {
                if (token != null) request.SetRequestHeader("Authorization", "Bearer " + token);
                object result = null;
                string problem = null;
                yield return Rest.Send(request, value => result = value, value => problem = value);
                if (allowMissing && request.responseCode == 404) done?.Invoke(null);
                else if (problem != null) Rest.Fail(error, problem);
                else done?.Invoke(result);
            }
        }

        private static T DecodeModel<T>(Dictionary<string, object> document)
        {
            var data = FirestoreValues.DecodeFields(Values.Get(document, "fields"));
            var path = Values.Text(document, "name");
            if (string.IsNullOrEmpty(path)) throw new FormatException("ドキュメント名がありません");
            var id = Uri.UnescapeDataString(path.Substring(path.LastIndexOf('/') + 1));
            if (typeof(T) == typeof(Drop)) return (T)(object)new Drop
            {
                id = id,
                authorId = Values.Text(data, "authorId"),
                authorName = Values.Text(data, "authorName"),
                teaser = Values.Text(data, "teaser"),
                lat = Values.Number(data, "lat") ?? 0,
                lng = Values.Number(data, "lng") ?? 0,
                radius = Values.Number(data, "radius") ?? 0,
                hasPhoto = Values.Flag(data, "hasPhoto"),
                openCount = Values.Long(data, "openCount"),
                likeCount = Values.OptionalLong(data, "likeCount"),
                commentCount = Values.OptionalLong(data, "commentCount"),
                createdAt = Values.Time(data, "createdAt"),
            };
            if (typeof(T) == typeof(OpenedEntry)) return (T)(object)new OpenedEntry
            {
                id = id,
                teaser = Values.Text(data, "teaser"),
                authorName = Values.Text(data, "authorName"),
                authorId = Values.Text(data, "authorId"),
                lat = Values.Number(data, "lat") ?? 0,
                lng = Values.Number(data, "lng") ?? 0,
                radius = Values.Number(data, "radius") ?? 0,
                openedAt = Values.Time(data, "openedAt"),
                openedDistance = Values.Number(data, "openedDistance") ?? 0,
            };
            if (typeof(T) == typeof(PlacedEntry)) return (T)(object)new PlacedEntry
            {
                id = id,
                teaser = Values.Text(data, "teaser"),
                lat = Values.Number(data, "lat") ?? 0,
                lng = Values.Number(data, "lng") ?? 0,
                radius = Values.Number(data, "radius") ?? 0,
                createdAt = Values.Time(data, "createdAt"),
            };
            if (typeof(T) == typeof(Profile)) return (T)(object)new Profile
            {
                name = Values.Text(data, "name"),
                placed = Values.OptionalLong(data, "placed"),
                opened = Values.OptionalLong(data, "opened"),
                opensReceived = Values.OptionalLong(data, "opensReceived"),
                likesReceived = Values.OptionalLong(data, "likesReceived"),
                comments = Values.OptionalLong(data, "comments"),
            };
            if (typeof(T) == typeof(Comment)) return (T)(object)new Comment
            {
                id = id,
                authorId = Values.Text(data, "authorId"),
                authorName = Values.Text(data, "authorName"),
                text = Values.Text(data, "text"),
                createdAt = Values.Time(data, "createdAt"),
            };
            if (typeof(T) == typeof(Notice)) return (T)(object)new Notice
            {
                id = id,
                type = Values.Text(data, "type"),
                dropId = Values.Text(data, "dropId"),
                teaser = Values.Text(data, "teaser"),
                actorId = Values.Text(data, "actorId"),
                actorName = Values.Text(data, "actorName"),
                text = Values.Text(data, "text"),
                createdAt = Values.Time(data, "createdAt"),
            };
            if (typeof(T) == typeof(KokoEvent)) return (T)(object)new KokoEvent
            {
                id = id,
                title = Values.Text(data, "title"),
                description = Values.Text(data, "description"),
                authorId = Values.Text(data, "authorId"),
                authorName = Values.Text(data, "authorName"),
                stops = Stops(data),
                startAt = Values.Time(data, "startAt"),
                endAt = Values.Time(data, "endAt"),
                participantCount = Values.Long(data, "participantCount"),
                createdAt = Values.Time(data, "createdAt"),
            };
            if (typeof(T) == typeof(Footprint)) return (T)(object)new Footprint
            {
                id = id,
                lat = Values.Number(data, "lat") ?? 0,
                lng = Values.Number(data, "lng") ?? 0,
                count = Values.Long(data, "count"),
            };
            if (typeof(T) == typeof(LikedEntry)) return (T)(object)new LikedEntry
            {
                id = id,
                createdAt = Values.Time(data, "createdAt"),
            };
            if (typeof(T) == typeof(JoinedEventEntry)) return (T)(object)new JoinedEventEntry
            {
                id = id,
                title = Values.Text(data, "title"),
                joinedAt = Values.Time(data, "joinedAt"),
            };
            throw new FormatException("未対応のモデルです: " + typeof(T).Name);
        }

        private static EventStop[] Stops(Dictionary<string, object> data)
        {
            if (Values.Get(data, "stops") == null) return Array.Empty<EventStop>();
            if (!(Values.Get(data, "stops") is object[] items)) throw new FormatException("stops が不正です");
            var stops = new EventStop[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var stop = Values.Map(items[i]);
                stops[i] = new EventStop
                {
                    id = Values.Text(stop, "id"),
                    teaser = Values.Text(stop, "teaser"),
                    lat = Values.Number(stop, "lat") ?? 0,
                    lng = Values.Number(stop, "lng") ?? 0,
                    radius = Values.Number(stop, "radius") ?? 0
                };
            }
            return stops;
        }
    }
}