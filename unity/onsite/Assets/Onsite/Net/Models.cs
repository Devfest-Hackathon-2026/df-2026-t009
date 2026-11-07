using System;

namespace Onsite.Net
{
    // フィールド名を Web と揃え、未設定の数値・日時は null のまま残す。
    [Serializable]
    public sealed class Drop
    {
        public string id;
        public string authorId;
        public string authorName;
        public string teaser;
        public double lat;
        public double lng;
        public double radius;
        public bool hasPhoto;
        public long openCount;
        public long? likeCount;
        public long? commentCount;
        public DateTime? createdAt;
    }

    [Serializable]
    public sealed class OpenedEntry
    {
        public string id;
        public string teaser;
        public string authorName;
        public string authorId;
        public double lat;
        public double lng;
        public double radius;
        public DateTime? openedAt;
        public double openedDistance;
    }

    [Serializable]
    public sealed class PlacedEntry
    {
        public string id;
        public string teaser;
        public double lat;
        public double lng;
        public double radius;
        public DateTime? createdAt;
    }

    [Serializable]
    public sealed class Profile
    {
        public string name;
        public long? placed;
        public long? opened;
        public long? opensReceived;
        public long? likesReceived;
        public long? comments;
    }

    [Serializable]
    public sealed class Comment
    {
        public string id;
        public string authorId;
        public string authorName;
        public string text;
        public DateTime? createdAt;
    }

    [Serializable]
    public sealed class Notice
    {
        public string id;
        public string type;
        public string dropId;
        public string teaser;
        public string actorId;
        public string actorName;
        public string text;
        public DateTime? createdAt;
    }

    [Serializable]
    public sealed class EventStop
    {
        public string id;
        public string teaser;
        public double lat;
        public double lng;
        public double radius;
    }

    [Serializable]
    public sealed class KokoEvent
    {
        public string id;
        public string title;
        public string description;
        public string authorId;
        public string authorName;
        public EventStop[] stops = Array.Empty<EventStop>();
        public DateTime? startAt;
        public DateTime? endAt;
        public long participantCount;
        public DateTime? createdAt;
    }

    [Serializable]
    public sealed class Footprint
    {
        public string id;
        public double lat;
        public double lng;
        public long count;
    }

    [Serializable]
    public sealed class LikedEntry
    {
        public string id;
        public DateTime? createdAt;
    }

    [Serializable]
    public sealed class JoinedEventEntry
    {
        public string id;
        public string title;
        public DateTime? joinedAt;
    }

}