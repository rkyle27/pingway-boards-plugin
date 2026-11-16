using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PingwayBoards;

/// <summary>
/// "Demo: sample posters": made-up posters built into the plugin (Demo/demo-posters.json, feed format v1, text only).
/// No network. Their times are relative to the moment they are loaded, so the events are always in the future.
/// </summary>
public static class DemoData
{
    public static List<Poster> Load()
    {
        try
        {
            using var stream = typeof(DemoData).Assembly.GetManifestResourceStream("PingwayBoards.Demo.demo-posters.json");
            if (stream == null)
                return new List<Poster>();
            using var reader = new StreamReader(stream);
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return FeedParser.ReadPosters(doc.RootElement, now);
        }
        catch (JsonException)
        {
            return new List<Poster>();
        }
    }
}
