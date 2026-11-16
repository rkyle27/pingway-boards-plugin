using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PingwayBoards;

/// <summary>A link on a poster (https only; shown as text, opened only after a warning).</summary>
public sealed class PosterLink
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

/// <summary>One poster of the Pingway Boards feed, format v1 (FEED-v1). All text is plain user text.</summary>
public sealed class Poster
{
    public long Id { get; init; }
    public string Kind { get; init; } = string.Empty; // lfm, lfs, fc, lffc, event
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string Playtime { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Dc { get; init; } = string.Empty;
    public string World { get; init; } = string.Empty;
    public IReadOnlyList<string> Worlds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Content { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Jobs { get; init; } = Array.Empty<string>();
    public string Schedule { get; init; } = string.Empty;
    public long EventAt { get; init; }
    public IReadOnlyList<PosterLink> Links { get; init; } = Array.Empty<PosterLink>();
    public string OwnerName { get; init; } = string.Empty;
    public long CreatedAt { get; init; }
    public long BumpedAt { get; init; }
    public string Url { get; init; } = string.Empty;
    public long Stars { get; init; }
    public int Featured { get; init; }
}

/// <summary>Reads the feed JSON (v1) into posters. Unknown fields are ignored, wrong types become empty values.</summary>
public static class FeedParser
{
    public const int FormatVersion = 1;

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) { "lfm", "lfs", "fc", "lffc", "event" };

    /// <summary>Reads the "posters" array. <paramref name="timeShift"/> is added to every time (the demo data uses times around 0).</summary>
    public static List<Poster> ReadPosters(JsonElement root, long timeShift = 0)
    {
        var list = new List<Poster>();
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("posters", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            var p = ReadPoster(el, timeShift);
            if (p != null)
                list.Add(p);
        }

        return list;
    }

    public static Poster? ReadPoster(JsonElement el, long timeShift = 0)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;
        var kind = Str(el, "kind", 10);
        if (!Kinds.Contains(kind))
            return null;
        var eventAt = Num(el, "eventAt");
        return new Poster
        {
            Id = Num(el, "id"),
            Kind = kind,
            Title = Str(el, "title", 200),
            Description = Str(el, "description", 5000),
            Host = Str(el, "host", 200),
            Playtime = Str(el, "playtime", 200),
            Region = Str(el, "region", 60),
            Dc = Str(el, "dc", 60),
            World = Str(el, "world", 60),
            Worlds = StrList(el, "worlds"),
            Content = StrList(el, "content"),
            Tags = StrList(el, "tags"),
            Roles = StrList(el, "roles"),
            Jobs = StrList(el, "jobs"),
            Schedule = Str(el, "schedule", 200),
            EventAt = kind == "event" && eventAt > 0 ? eventAt + timeShift : 0,
            Links = LinkList(el),
            OwnerName = Str(el, "ownerName", 100),
            CreatedAt = Num(el, "createdAt") + timeShift,
            BumpedAt = Num(el, "bumpedAt") + timeShift,
            Url = Str(el, "url", 300),
            Stars = Math.Max(0, Num(el, "stars")),
            Featured = (int)Math.Clamp(Num(el, "featured"), 0, 3),
        };
    }

    /// <summary>The "v" of a feed answer, or -1 when there is none.</summary>
    public static long Version(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("v", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : -1;

    /// <summary>The "code" of an error answer, or "" when there is none.</summary>
    public static string ErrorCode(JsonElement root) => root.ValueKind == JsonValueKind.Object ? Str(root, "code", 40) : string.Empty;

    public static long Num(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number)
            return 0;
        if (v.TryGetInt64(out var n))
            return n;
        return v.TryGetDouble(out var d) && double.IsFinite(d) ? (long)Math.Clamp(d, long.MinValue / 2.0, long.MaxValue / 2.0) : 0;
    }

    public static string Str(JsonElement el, string name, int max)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return string.Empty;
        return Clean(v.GetString(), max);
    }

    private static IReadOnlyList<string> StrList(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new List<string>();
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && list.Count < 40)
            {
                var s = Clean(item.GetString(), 60);
                if (s.Length > 0)
                    list.Add(s);
            }
        }

        return list;
    }

    private static IReadOnlyList<PosterLink> LinkList(JsonElement el)
    {
        if (!el.TryGetProperty("links", out var v) || v.ValueKind != JsonValueKind.Array)
            return Array.Empty<PosterLink>();
        var list = new List<PosterLink>();
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || list.Count >= 3)
                continue;
            var url = Str(item, "url", 300);
            if (UrlSafety.SafeExternal(url) == null)
                continue; // https only, everything else is dropped
            list.Add(new PosterLink { Title = Str(item, "title", 80), Url = url });
        }

        return list;
    }

    /// <summary>Plain text only: line breaks kept as \n, every other control character removed, length capped.</summary>
    public static string Clean(string? s, int max)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        var sb = new StringBuilder(Math.Min(s.Length, max));
        foreach (var ch in s.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            if (sb.Length >= max)
                break;
            if (ch == '\n')
                sb.Append('\n');
            else if (ch == '\t')
                sb.Append(' ');
            else if (!char.IsControl(ch) && ch != '‮' && ch != '‭')
                sb.Append(ch);
        }

        return sb.ToString().Trim();
    }
}
