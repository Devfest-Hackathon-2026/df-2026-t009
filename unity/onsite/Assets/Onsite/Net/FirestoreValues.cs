using System;
using System.Collections.Generic;
using System.Globalization;

namespace Onsite.Net
{
    public static class FirestoreValues
    {
        public static Dictionary<string, object> DecodeFields(object fields)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (fields == null) return result;
            foreach (var pair in Values.Map(fields)) result.Add(pair.Key, Decode(pair.Value));
            return result;
        }

        public static object Decode(object typedValue)
        {
            var value = Values.Map(typedValue);
            if (value.Count != 1) throw new FormatException("Firestore 値の型が不正です");
            if (value.ContainsKey("nullValue")) return null;
            if (value.TryGetValue("stringValue", out var text))
                return text as string ?? throw new FormatException("stringValue が不正です");
            if (value.TryGetValue("integerValue", out var integer))
            {
                // Firestore の整数は JSON 文字列なので double を経由しない。
                if (!(integer is string digits) || !long.TryParse(digits, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var n)) throw new FormatException("integerValue が不正です");
                return n;
            }
            if (value.ContainsKey("doubleValue"))
                return Values.Number(value, "doubleValue") ?? throw new FormatException("doubleValue が不正です");
            if (value.TryGetValue("booleanValue", out var flag))
                return flag is bool ? flag : throw new FormatException("booleanValue が不正です");
            if (value.TryGetValue("timestampValue", out var timestamp))
            {
                if (!(timestamp is string iso)) throw new FormatException("timestampValue が不正です");
                // Firestore のナノ秒を .NET の 100ns 精度に切り詰める。
                var dot = iso.IndexOf('.');
                if (dot >= 0)
                {
                    var end = dot + 1;
                    while (end < iso.Length && char.IsDigit(iso[end])) end++;
                    if (end - dot - 1 > 7) iso = iso.Remove(dot + 8, end - dot - 8);
                }
                if (!DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time))
                    throw new FormatException("timestampValue が不正です");
                return time.UtcDateTime;
            }
            if (value.TryGetValue("arrayValue", out var array))
            {
                var entries = Values.Get(Values.Map(array), "values");
                if (entries == null) return Array.Empty<object>();
                if (!(entries is List<object> list)) throw new FormatException("arrayValue が不正です");
                var items = new object[list.Count];
                for (var i = 0; i < list.Count; i++) items[i] = Decode(list[i]);
                return items;
            }
            if (value.TryGetValue("mapValue", out var map))
                return DecodeFields(Values.Get(Values.Map(map), "fields"));
            throw new FormatException("未対応の Firestore 値です");
        }
    }
}