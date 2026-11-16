using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PingwayBoards;

/// <summary>
/// The plugin texts in English, French ("tu"), German ("du") and Japanese (Lang/*.json, embedded in the plugin).
/// The language follows the game client language; anything missing falls back to English.
/// </summary>
public sealed class Loc
{
    private readonly Dictionary<string, string> texts;
    private readonly Dictionary<string, string> english;

    public string Code { get; }
    public CultureInfo Culture { get; }

    /// <param name="clientLanguage">The game client language name (Japanese, English, German, French).</param>
    public Loc(string clientLanguage)
    {
        this.Code = clientLanguage switch
        {
            "Japanese" => "ja",
            "German" => "de",
            "French" => "fr",
            _ => "en",
        };
        this.Culture = this.Code switch
        {
            "ja" => SafeCulture("ja-JP"),
            "de" => SafeCulture("de-DE"),
            "fr" => SafeCulture("fr-FR"),
            _ => SafeCulture("en-US"),
        };
        this.english = Load("en");
        this.texts = this.Code == "en" ? this.english : Load(this.Code);
    }

    private static CultureInfo SafeCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static Dictionary<string, string> Load(string code)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream("PingwayBoards.Lang." + code + ".json");
            if (stream == null)
                return result;
            using var reader = new StreamReader(stream);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
            if (parsed != null)
            {
                foreach (var pair in parsed)
                    result[pair.Key] = pair.Value;
            }
        }
        catch (JsonException)
        {
            // a broken language file: fall back to English (and then to the key itself)
        }

        return result;
    }

    /// <summary>The text for a key.</summary>
    public string T(string key)
    {
        if (this.texts.TryGetValue(key, out var value))
            return value;
        if (this.english.TryGetValue(key, out value))
            return value;
        return key;
    }

    /// <summary>The text for a key with {name} placeholders filled in.</summary>
    public string T(string key, params (string Name, string Value)[] vars)
    {
        var text = this.T(key);
        foreach (var (name, value) in vars)
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        return text;
    }

    /// <summary>Plural texts: key_one / key_other with {n}.</summary>
    public string Count(string key, int n)
    {
        // French counts 0 and 1 as singular; English and German only 1; Japanese has one form
        var one = this.Code == "fr" ? n <= 1 : n == 1;
        return this.T(key + (one ? "_one" : "_other"), ("n", n.ToString(this.Culture)));
    }
}
