using System;
using System.Collections;
using System.Collections.Generic;

namespace Onsite.Net
{
    public sealed class Callable
    {
        private readonly FirebaseConfig config;
        private readonly FirebaseAuthRest auth;

        public Callable(FirebaseConfig config, FirebaseAuthRest auth)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        [Serializable]
        public sealed class FootprintPoint
        {
            public double lat;
            public double lng;
        }

        [Serializable]
        public sealed class CreateDropRequest
        {
            public string teaser;
            public string body;
            public double radius;
            public double lat;
            public double lng;
            public string authorName;
            public string photoPath;
        }

        [Serializable]
        public sealed class CreateDropResponse
        {
            public string id;
        }

        [Serializable]
        public sealed class OpenDropRequest
        {
            public string dropId;
            public double? lat;
            public double? lng;
            public double? accuracy;
            public string name;
        }

        [Serializable]
        public sealed class OpenDropResponse
        {
            public string status;
            public bool firstTime;
            public double? distance;
            public double radius;
            public string body;
            public string photo;
        }

        [Serializable]
        public sealed class AddCommentRequest
        {
            public string dropId;
            public string text;
            public string name;
        }

        [Serializable]
        public sealed class AddCommentResponse
        {
            public string id;
        }

        [Serializable]
        public sealed class ToggleLikeRequest
        {
            public string dropId;
            public string name;
        }

        [Serializable]
        public sealed class ToggleLikeResponse
        {
            public bool liked;
        }

        [Serializable]
        public sealed class DeleteDropRequest
        {
            public string dropId;
        }

        [Serializable]
        public sealed class DeleteDropResponse
        {
            public bool ok;
        }

        [Serializable]
        public sealed class CreateEventRequest
        {
            public string title;
            public string description;
            public string[] dropIds;
            public long? startAt;
            public long? endAt;
            public string name;
        }

        [Serializable]
        public sealed class CreateEventResponse
        {
            public string id;
        }

        [Serializable]
        public sealed class JoinEventRequest
        {
            public string eventId;
            public bool join = true;
        }

        [Serializable]
        public sealed class JoinEventResponse
        {
            public bool joined;
        }

        [Serializable]
        public sealed class DeleteEventRequest
        {
            public string eventId;
        }

        [Serializable]
        public sealed class DeleteEventResponse
        {
            public bool ok;
        }

        [Serializable]
        public sealed class AddFootprintsRequest
        {
            public FootprintPoint[] points;
        }

        [Serializable]
        public sealed class AddFootprintsResponse
        {
            public int cells;
        }

        // data/result は辞書として組み立て、IL2CPP でもリフレクションに依存しない。
        private IEnumerator Send<T>(string name, Dictionary<string, object> data,
            Func<Dictionary<string, object>, T> decode, Action<T> done, Action<string> error)
        {
            string token = null;
            yield return auth.GetIdToken(value => token = value);
            if (token == null)
            {
                Rest.Fail(error, auth.LastError ?? "ログインが必要です");
                yield break;
            }
            object response = null;
            string problem = null;
            var url = "https://" + config.region + "-" + config.projectId + ".cloudfunctions.net/" + name;
            using (var request = Rest.JsonRequest(url, "POST",
                new Dictionary<string, object> { { "data", data } }))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
                yield return Rest.Send(request, value => response = value, value => problem = value);
            }
            T result = default;
            if (problem == null)
            {
                try
                {
                    var envelope = Values.Map(response);
                    if (!envelope.TryGetValue("result", out var payload))
                        throw new FormatException("callable の result がありません");
                    result = decode(Values.Map(payload));
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    problem = e.Message;
                }
            }
            if (problem != null) Rest.Fail(error, problem);
            else done?.Invoke(result);
        }

        public IEnumerator CreateDrop(CreateDropRequest input, Action<CreateDropResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("createDrop", new Dictionary<string, object>
            {
                { "teaser", input.teaser },
                { "body", input.body },
                { "radius", input.radius },
                { "lat", input.lat },
                { "lng", input.lng },
                { "authorName", input.authorName },
                { "photoPath", input.photoPath },
            }, data => new CreateDropResponse
            {
                id = Values.Text(data, "id"),
            }, done, error);
        }

        public IEnumerator OpenDrop(OpenDropRequest input, Action<OpenDropResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("openDrop", new Dictionary<string, object>
            {
                { "dropId", input.dropId },
                { "lat", input.lat },
                { "lng", input.lng },
                { "accuracy", input.accuracy },
                { "name", input.name },
            }, data => new OpenDropResponse
            {
                status = Values.Text(data, "status"),
                firstTime = Values.Flag(data, "firstTime"),
                distance = Values.Number(data, "distance"),
                radius = Values.Number(data, "radius") ?? 0,
                body = Values.Text(data, "body"),
                photo = Values.Text(data, "photo"),
            }, done, error);
        }

        public IEnumerator AddComment(AddCommentRequest input, Action<AddCommentResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("addComment", new Dictionary<string, object>
            {
                { "dropId", input.dropId },
                { "text", input.text },
                { "name", input.name },
            }, data => new AddCommentResponse
            {
                id = Values.Text(data, "id"),
            }, done, error);
        }

        public IEnumerator ToggleLike(ToggleLikeRequest input, Action<ToggleLikeResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("toggleLike", new Dictionary<string, object>
            {
                { "dropId", input.dropId },
                { "name", input.name },
            }, data => new ToggleLikeResponse
            {
                liked = Values.Flag(data, "liked"),
            }, done, error);
        }

        public IEnumerator DeleteDrop(DeleteDropRequest input, Action<DeleteDropResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("deleteDrop", new Dictionary<string, object>
            {
                { "dropId", input.dropId },
            }, data => new DeleteDropResponse
            {
                ok = Values.Flag(data, "ok"),
            }, done, error);
        }

        public IEnumerator CreateEvent(CreateEventRequest input, Action<CreateEventResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            // startAt/endAt は UTC の Unix ミリ秒。未指定なら null を送る。
            return Send("createEvent", new Dictionary<string, object>
            {
                { "title", input.title },
                { "description", input.description },
                { "dropIds", input.dropIds },
                { "startAt", input.startAt },
                { "endAt", input.endAt },
                { "name", input.name },
            }, data => new CreateEventResponse
            {
                id = Values.Text(data, "id"),
            }, done, error);
        }

        public IEnumerator JoinEvent(JoinEventRequest input, Action<JoinEventResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("joinEvent", new Dictionary<string, object>
            {
                { "eventId", input.eventId },
                { "join", input.join },
            }, data => new JoinEventResponse
            {
                joined = Values.Flag(data, "joined"),
            }, done, error);
        }

        public IEnumerator DeleteEvent(DeleteEventRequest input, Action<DeleteEventResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Send("deleteEvent", new Dictionary<string, object>
            {
                { "eventId", input.eventId },
            }, data => new DeleteEventResponse
            {
                ok = Values.Flag(data, "ok"),
            }, done, error);
        }

        public IEnumerator AddFootprints(AddFootprintsRequest input, Action<AddFootprintsResponse> done,
            Action<string> error = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            List<object> points = null;
            if (input.points != null)
            {
                points = new List<object>();
                foreach (var point in input.points)
                    points.Add(point == null ? null : new Dictionary<string, object>
                    {
                        { "lat", point.lat }, { "lng", point.lng }
                    });
            }
            return Send("addFootprints", new Dictionary<string, object>
            {
                { "points", points },
            }, data => new AddFootprintsResponse
            {
                cells = checked((int)Values.Long(data, "cells")),
            }, done, error);
        }

    }
}