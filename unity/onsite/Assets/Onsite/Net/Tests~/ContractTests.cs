using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Onsite.Net;
using UnityEngine;
using UnityEngine.Networking;

internal static class ContractTests
{
    private static int assertions;
    private static FirebaseConfig Config() => new FirebaseConfig
    {
        apiKey = "test-only", projectId = "test-project", storageBucket = "test-bucket"
    };

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void Equal(object expected, object actual, string message) =>
        Check(Equals(expected, actual), message + " (expected " + expected + ", got " + actual + ")");

    private sealed class Routine
    {
        private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        public Routine(IEnumerator routine) { stack.Push(routine); }
        public bool Step()
        {
            for (var i = 0; i < 1000 && stack.Count > 0; i++)
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                if (stack.Peek().Current is IEnumerator nested) stack.Push(nested);
                else return true;
            }
            return stack.Count > 0;
        }
        public void Finish()
        {
            for (var i = 0; i < 1000; i++) if (!Step()) return;
            throw new Exception("Coroutine did not complete");
        }
    }

    private static void Run(IEnumerator value) => new Routine(value).Finish();
    private static void Reply(string json, Action<UnityWebRequest> inspect = null, int code = 200)
    {
        UnityWebRequest.Responses.Enqueue(request =>
        {
            inspect?.Invoke(request);
            request.responseCode = code;
            request.result = code < 400 ? UnityWebRequest.Result.Success : UnityWebRequest.Result.ProtocolError;
            request.error = code < 400 ? null : "HTTP " + code;
            request.downloadHandler.text = json;
        });
    }

    private static Dictionary<string, object> Body(UnityWebRequest request) =>
        Values.Map(Json.Parse(Encoding.UTF8.GetString(request.uploadHandler.data)));

    private static FirebaseAuthRest SignedIn()
    {
        PlayerPrefs.Data.Clear();
        var auth = new FirebaseAuthRest(Config());
        Reply("{\"localId\":\"u1\",\"idToken\":\"id1\",\"refreshToken\":\"refresh1\",\"expiresIn\":\"3600\"}",
            request =>
            {
                Check(request.url.EndsWith("accounts:signUp?key=test-only"), "signup URL");
                Equal(true, Body(request)["returnSecureToken"], "signup payload");
            });
        Run(auth.EnsureSignedIn());
        Equal("u1", auth.Uid, "signup UID");
        Equal("refresh1", PlayerPrefs.GetString("onsite.refresh", ""), "refresh persisted");
        Check(auth.IsSignedIn, "signed in");
        return auth;
    }

    private static object Decode(string json) => FirestoreValues.Decode(Json.Parse(json));

    private static void JsonContracts()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var input = new Dictionary<string, object>
        {
            { "text", "日本語\n\"\\\t" }, { "n", 1.25 }, { "nil", null },
            { "array", new object[] { true, long.MaxValue, -2.5e-10 } }
        };
        var roundtrip = Values.Map(Json.Parse(Json.Stringify(input)));
        Equal(input["text"], roundtrip["text"], "escaped Unicode round trip");
        Equal(1.25, roundtrip["n"], "invariant number");
        Equal(long.MaxValue, ((List<object>)roundtrip["array"])[1], "integer precision");
        Equal("😀", Json.Parse("\"\\ud83d\\ude00\""), "surrogate pair");
        foreach (var invalid in new[] { "", "01", "1.", "1e", "-", "[1,]", "{\"x\":1,}", "true false",
            "{\"x\":1,\"x\":2}", "\"\\q\"", "\"bad\n\"", new string('[', 66) + new string(']', 66) })
        {
            var rejected = false;
            try { Json.Parse(invalid); } catch (FormatException) { rejected = true; }
            Check(rejected, "reject malformed JSON: " + invalid);
        }
        var nanRejected = false;
        try { Json.Stringify(double.NaN); } catch (FormatException) { nanRejected = true; }
        Check(nanRejected, "reject nonfinite JSON");
        Equal(long.MaxValue, Decode("{\"integerValue\":\"9223372036854775807\"}"), "Firestore 64-bit integer");
        Equal(12.5, Decode("{\"doubleValue\":12.5}"), "Firestore double");
        Equal(true, Decode("{\"booleanValue\":true}"), "Firestore bool");
        Equal(null, Decode("{\"nullValue\":null}"), "Firestore null");
        Equal(0, ((object[])Decode("{\"arrayValue\":{}}")).Length, "Firestore empty array");
        Equal(0, ((Dictionary<string, object>)Decode("{\"mapValue\":{}}")).Count, "Firestore empty map");
        var timestamp = (DateTime)Decode("{\"timestampValue\":\"2026-11-08T01:02:03.123456789Z\"}");
        Equal(DateTimeKind.Utc, timestamp.Kind, "timestamp UTC");
        Equal(1234567L, timestamp.Ticks % TimeSpan.TicksPerSecond, "nanosecond truncation");
        var offsetTime = (DateTime)Decode("{\"timestampValue\":\"2026-11-08T10:02:03+09:00\"}");
        Equal(1, offsetTime.Hour, "timezone normalization");
    }

    private static void AuthContracts()
    {
        var auth = SignedIn();
        string token = null;
        Run(auth.GetIdToken(value => token = value));
        Equal("id1", token, "cached token");
        typeof(FirebaseAuthRest).GetField("expiresAt", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(auth, DateTime.UtcNow.AddMinutes(4));
        Reply("{\"user_id\":\"u1\",\"id_token\":\"id2\",\"refresh_token\":\"refresh2\",\"expires_in\":\"3600\"}",
            request =>
            {
                Check(request.url.Contains("securetoken.googleapis.com"), "refresh URL");
                Equal("refresh_token", request.form.fields["grant_type"], "refresh grant");
                Equal("refresh1", request.form.fields["refresh_token"], "refresh form");
            });
        var first = new Routine(auth.GetIdToken(value => token = value));
        first.Step();
        string secondToken = null;
        var second = new Routine(auth.GetIdToken(value => secondToken = value));
        second.Step();
        first.Finish();
        second.Finish();
        Equal("id2", token, "refreshed token");
        Equal("id2", secondToken, "concurrent refresh shares token");
        var restored = new FirebaseAuthRest(Config());
        Equal("u1", restored.Uid, "restore UID");
        Reply("{\"user_id\":\"u1\",\"id_token\":\"id3\",\"refresh_token\":\"refresh3\",\"expires_in\":\"3600\"}");
        var pending = new Routine(restored.GetIdToken(value => token = value));
        pending.Step();
        restored.SignOutAndForget();
        pending.Finish();
        Equal(null, token, "signout cancels pending token");
        Equal("", restored.Uid, "no stale UID");
        Check(!PlayerPrefs.Data.ContainsKey("onsite.refresh"), "no stale credentials");

        PlayerPrefs.SetString("onsite.uid", "old");
        PlayerPrefs.SetString("onsite.refresh", "saved");
        restored = new FirebaseAuthRest(Config());
        string problem = null;
        restored.Error += value => problem = value;
        Reply("{\"error\":{\"message\":\"NETWORK_DOWN\"}}", code: 503);
        Run(restored.EnsureSignedIn());
        Equal("NETWORK_DOWN", problem, "auth server error surfaced");
        Equal("saved", PlayerPrefs.GetString("onsite.refresh", ""), "transient error retains identity");
        Reply("{\"error\":{\"message\":\"INVALID_REFRESH_TOKEN\"}}", code: 400);
        Reply("{\"localId\":\"new\",\"idToken\":\"new-id\",\"refreshToken\":\"new-refresh\",\"expiresIn\":\"3600\"}");
        Run(restored.EnsureSignedIn());
        Equal("new", restored.Uid, "invalid refresh recreates anonymous account");
        var notStarted = restored.GetIdToken(value => token = value);
        restored.SignOutAndForget();
        Run(notStarted);
        Equal(null, token, "signout cancels a token coroutine before its first step");
    }

    private static void CallableContracts(FirebaseAuthRest auth)
    {
        var functions = new Callable(Config(), auth);
        Reply("{\"result\":{\"status\":\"opened\",\"firstTime\":false,\"distance\":null,\"radius\":100,\"body\":\"本文\",\"photo\":null}}",
            request =>
            {
                Equal("Bearer id1", request.headers["Authorization"], "callable auth");
                Equal("application/json; charset=utf-8", request.headers["Content-Type"], "callable content type");
                Check(request.url.EndsWith("asia-northeast1-test-project.cloudfunctions.net/openDrop"), "callable URL");
                var data = Values.Map(Body(request)["data"]);
                Equal("drop1", data["dropId"], "callable input");
                Equal(null, data["lat"], "optional location");
            });
        Callable.OpenDropResponse opened = null;
        Run(functions.OpenDrop(new Callable.OpenDropRequest { dropId = "drop1" }, value => opened = value));
        Equal(null, opened.distance, "nullable distance");
        Equal("本文", opened.body, "Japanese body");
        Reply("{\"result\":{\"status\":\"too_far\",\"distance\":120,\"radius\":100}}");
        Run(functions.OpenDrop(new Callable.OpenDropRequest { dropId = "drop1", lat = 35, lng = 139 },
            value => opened = value));
        Equal("too_far", opened.status, "too far branch");
        Equal(120d, opened.distance, "too far distance");
        string error = null;
        Reply("{\"error\":{\"status\":\"PERMISSION_DENIED\",\"message\":\"この場所でひらいた人だけが反応できます\"}}",
            code: 403);
        Run(functions.AddComment(new Callable.AddCommentRequest { dropId = "drop1", text = "文", name = "名" },
            _ => throw new Exception("Error became success"), value => error = value));
        Equal("この場所でひらいた人だけが反応できます", error, "Japanese error unchanged");
        Reply("{\"result\":{\"id\":\"event1\"}}", request =>
        {
            var data = Values.Map(Body(request)["data"]);
            Equal(1794096000000L, data["startAt"], "event Unix milliseconds");
            Equal(null, data["endAt"], "optional event time");
        });
        Run(functions.CreateEvent(new Callable.CreateEventRequest
        {
            title = "巡り", dropIds = new[] { "a", "b" }, startAt = 1794096000000L
        }, value => Equal("event1", value.id, "create event response")));
        Reply("{\"result\":{\"cells\":1}}", request =>
        {
            var points = (List<object>)Values.Map(Body(request)["data"])["points"];
            Equal(35.25, Values.Map(points[0])["lat"], "footprint coordinates");
        });
        Run(functions.AddFootprints(new Callable.AddFootprintsRequest
        {
            points = new[] { new Callable.FootprintPoint { lat = 35.25, lng = 139.5 } }
        }, value => Equal(1, value.cells, "footprint response")));
        Reply("{\"result\":{\"id\":\"new-drop\"}}", request =>
        {
            Check(request.url.EndsWith("/createDrop"), "createDrop function");
            Equal("uploads/u1/image.jpg", Values.Map(Body(request)["data"])["photoPath"], "photo path input");
        });
        Run(functions.CreateDrop(new Callable.CreateDropRequest { photoPath = "uploads/u1/image.jpg" },
            value => Equal("new-drop", value.id, "createDrop result")));
        Reply("{\"result\":{\"liked\":false}}", request => Check(request.url.EndsWith("/toggleLike"), "toggleLike function"));
        Run(functions.ToggleLike(new Callable.ToggleLikeRequest { dropId = "d1" },
            value => Equal(false, value.liked, "toggleLike result")));
        Reply("{\"result\":{\"ok\":true}}", request => Check(request.url.EndsWith("/deleteDrop"), "deleteDrop function"));
        Run(functions.DeleteDrop(new Callable.DeleteDropRequest { dropId = "d1" },
            value => Equal(true, value.ok, "deleteDrop result")));
        Reply("{\"result\":{\"joined\":true}}", request =>
        {
            Equal(true, Values.Map(Body(request)["data"])["join"], "joinEvent defaults to joining");
        });
        Run(functions.JoinEvent(new Callable.JoinEventRequest { eventId = "e1" },
            value => Equal(true, value.joined, "joinEvent result")));
        Reply("{\"result\":{\"ok\":true}}", request => Check(request.url.EndsWith("/deleteEvent"), "deleteEvent function"));
        Run(functions.DeleteEvent(new Callable.DeleteEventRequest { eventId = "e1" },
            value => Equal(true, value.ok, "deleteEvent result")));
    }

    private static void FirestoreContracts(FirebaseAuthRest auth)
    {
        var firestore = new FirestoreRest(Config(), auth);
        Reply("[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/drops/d1\",\"fields\":{\"teaser\":{\"stringValue\":\"予告\"},\"openCount\":{\"integerValue\":\"9223372036854775807\"},\"lat\":{\"doubleValue\":35.5},\"hasPhoto\":{\"booleanValue\":true},\"createdAt\":{\"timestampValue\":\"2026-11-08T00:00:00Z\"}}}},{\"readTime\":\"2026-11-08T00:00:00Z\"}]",
            request =>
            {
                Check(request.url.EndsWith("/documents:runQuery"), "root query URL");
                var query = Values.Map(Body(request)["structuredQuery"]);
                Equal(300L, query["limit"], "drop query limit");
                var order = Values.Map(((List<object>)query["orderBy"])[0]);
                Equal("DESCENDING", order["direction"], "drop query order");
                Equal("createdAt", Values.Map(order["field"])["fieldPath"], "drop query field");
            });
        Run(firestore.ListDrops(300, drops =>
        {
            Equal(1, drops.Length, "skip metadata rows");
            Equal("d1", drops[0].id, "document ID");
            Equal(long.MaxValue, drops[0].openCount, "model integer precision");
            Equal(null, drops[0].likeCount, "optional counts");
            Equal(DateTimeKind.Utc, drops[0].createdAt.Value.Kind, "model UTC");
        }));
        Reply("[{\"readTime\":\"2026-11-08T00:00:00Z\"}]", request =>
        {
            Check(request.url.EndsWith("/users/u1:runQuery"), "own query URL");
            Check(request.headers.ContainsKey("Authorization"), "own query auth");
        });
        Run(firestore.ListOwn<OpenedEntry>("u1", "opened", "openedAt",
            values => Equal(0, values.Length, "empty private query")));
        Reply("[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/events/e1\",\"fields\":{\"stops\":{\"arrayValue\":{\"values\":[{\"mapValue\":{\"fields\":{\"id\":{\"stringValue\":\"d1\"},\"lat\":{\"doubleValue\":35.5},\"radius\":{\"integerValue\":\"100\"}}}}]}},\"startAt\":{\"nullValue\":null}}}}]");
        Run(firestore.ListEvents(events =>
        {
            Equal("d1", events[0].stops[0].id, "nested event stops");
            Equal(100d, events[0].stops[0].radius, "integer coordinate conversion");
            Equal(null, events[0].startAt, "nullable timestamp");
        }));
        Reply("[]", request =>
        {
            var query = Values.Map(Body(request)["structuredQuery"]);
            var filters = (List<object>)Values.Map(Values.Map(query["where"])["compositeFilter"])["filters"];
            Equal(2, filters.Count, "latitude bounds");
            Equal("GREATER_THAN_OR_EQUAL", Values.Map(Values.Map(filters[0])["fieldFilter"])["op"], "lower bound");
            Equal(800L, query["limit"], "footprint limit");
        });
        Run(firestore.ListFootprints(35, 36, _ => { }));
        Reply("{\"error\":{\"message\":\"Missing\"}}", code: 404);
        Run(firestore.GetProfile("missing", profile => Equal(null, profile.name, "missing profile")));
        Reply("[]", request =>
        {
            Check(request.url.EndsWith("/drops/d1:runQuery"), "comments parent");
            var order = Values.Map(((List<object>)Values.Map(Body(request)["structuredQuery"])["orderBy"])[0]);
            Equal("ASCENDING", order["direction"], "comment ascending order");
        });
        Run(firestore.ListComments("d1", _ => { }));
        Reply("[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/users/u1/liked/d1\",\"fields\":{}}}]");
        Run(firestore.ListOwn<LikedEntry>("u1", "liked", "createdAt",
            liked => Equal("d1", liked[0].id, "liked model")));
        Reply("[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/users/u1/events/e1\",\"fields\":{\"title\":{\"stringValue\":\"巡り\"}}}}]");
        Run(firestore.ListOwn<JoinedEventEntry>("u1", "events", "joinedAt",
            events => Equal("巡り", events[0].title, "joined event model")));
        Reply("[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/users/u1/placed/d1\",\"fields\":{\"radius\":{\"integerValue\":\"300\"}}}}]");
        Run(firestore.ListOwn<PlacedEntry>("u1", "placed", "createdAt",
            entries => Equal(300d, entries[0].radius, "placed model")));
        Reply("[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/users/u1/notifications/n1\",\"fields\":{\"type\":{\"stringValue\":\"like\"}}}}]");
        Run(firestore.ListOwn<Notice>("u1", "notifications", "createdAt",
            notices => Equal("like", notices[0].type, "notice model")));

        var otherAuth = new FirebaseAuthRest(Config());
        var pendingReader = new FirestoreRest(Config(), otherAuth);
        string cancelled = null;
        var pendingOwn = pendingReader.ListOwn<OpenedEntry>("u1", "opened", "openedAt",
            _ => throw new Exception("Stale own query succeeded"), error => cancelled = error);
        otherAuth.SignOutAndForget();
        Run(pendingOwn);
        Check(cancelled != null, "signout cancels unstarted private query without signing in");
        var publicReader = new FirestoreRest(Config(), new FirebaseAuthRest(Config()));
        // サインアウト後の公開クエリは匿名認証を始めない。
        PlayerPrefs.Data.Clear();
        publicReader = new FirestoreRest(Config(), new FirebaseAuthRest(Config()));
        Reply("[]", request => Check(!request.headers.ContainsKey("Authorization"), "public query without token"));
        Run(publicReader.ListDrops(1, _ => { }));
    }

    private static void StorageContracts(FirebaseAuthRest auth)
    {
        var storage = new Storage(Config(), auth);
        var jpeg = new byte[] { 0xff, 0xd8, 0xff, 0xd9 };
        Reply("{\"name\":\"uploaded\"}", request =>
        {
            Check(request.url.Contains("/v0/b/test-bucket/o?uploadType=media&name=uploads%2Fu1%2F"), "storage URL");
            Equal("image/jpeg", request.headers["Content-Type"], "storage type");
            Equal("Bearer id1", request.headers["Authorization"], "storage bearer");
            Check(ReferenceEquals(jpeg, request.uploadHandler.data), "raw JPEG bytes");
        });
        Run(storage.UploadJpeg(jpeg, path =>
        {
            Check(path.StartsWith("uploads/u1/") && path.EndsWith(".jpg"), "returned storage object path");
        }));
        string problem = null;
        Run(storage.UploadJpeg(new byte[] { 1, 2 }, _ => throw new Exception("Invalid JPEG uploaded"), value => problem = value));
        Check(problem != null, "invalid JPEG rejected locally");
    }

    private static void FacadeContracts()
    {
        PlayerPrefs.Data.Clear();
        var api = new OnsiteApi();
        typeof(OnsiteApi).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(api, Config());
        typeof(OnsiteApi).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(api, null);
        Equal(api, OnsiteApi.Instance, "facade singleton");
        api.Error += error => throw new Exception(error);
        Reply("{\"localId\":\"u1\",\"idToken\":\"id1\",\"refreshToken\":\"refresh1\",\"expiresIn\":\"3600\"}");
        Run(api.EnsureSignedIn());
        var dropChanges = 0;
        var logChanges = 0;
        var noticeChanges = 0;
        api.DropsChanged += () => dropChanges++;
        api.MyLogChanged += () => logChanges++;
        api.NoticesChanged += () => noticeChanges++;
        Reply("[]");
        Reply("[]", request => Check(Encoding.UTF8.GetString(request.uploadHandler.data).Contains("openedAt"), "poll opened"));
        Reply("[]", request => Check(Encoding.UTF8.GetString(request.uploadHandler.data).Contains("placed"), "poll placed"));
        Reply("[]", request => Check(Encoding.UTF8.GetString(request.uploadHandler.data).Contains("notifications"), "poll notices"));
        Reply("[]", request => Check(Encoding.UTF8.GetString(request.uploadHandler.data).Contains("liked"), "poll liked"));
        Run(api.Refresh());
        Equal(1, dropChanges, "drops event");
        Equal(1, logChanges, "log event");
        Equal(1, noticeChanges, "notices event");
        api.SignOutAndForget();
        Equal(0, api.Opened.Count, "signout clears opened");
        Equal(0, api.Placed.Count, "signout clears placed");
        Equal(0, api.Liked.Count, "signout clears liked");
        Equal(0, api.Notices.Count, "signout clears notices");
        Reply("[]", request => Check(!request.headers.ContainsKey("Authorization"), "signed-out poll stays public"));
        Run(api.Refresh());
        Equal("", api.Auth.Uid, "poll does not sign in after explicit signout");
        Equal(2, logChanges, "signout log event");
        Equal(2, noticeChanges, "signout notices event");
        typeof(OnsiteApi).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(api, null);
        Equal(null, OnsiteApi.Instance, "singleton cleanup");
    }

    public static int Main()
    {
        JsonContracts();
        AuthContracts();
        var auth = SignedIn();
        CallableContracts(auth);
        FirestoreContracts(auth);
        StorageContracts(auth);
        FacadeContracts();
        Check(UnityWebRequest.Responses.Count == 0, "all expected requests consumed");
        Console.WriteLine("C# contract tests passed: " + assertions + " assertions");
        return 0;
    }
}