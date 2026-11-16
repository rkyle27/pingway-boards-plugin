using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PingwayBoards;

/// <summary>The fixed lists and the filter / sort rules of the website's Boards (same behaviour as pingway.app).</summary>
public static class Boards
{
    public static readonly string[] Ultimates = { "ucob", "uwu", "tea", "dsr", "top", "fru", "dmu" };
    public static readonly string[] Savage = { "m9s", "m10s", "m11s", "m12s" };
    public static readonly string[] RoleOrder = { "tank", "healer", "melee", "ranged", "caster" };

    public static readonly Dictionary<string, string[]> RoleJobs = new(StringComparer.Ordinal)
    {
        ["tank"] = new[] { "pld", "war", "drk", "gnb" },
        ["healer"] = new[] { "whm", "sch", "ast", "sge" },
        ["melee"] = new[] { "mnk", "drg", "nin", "sam", "rpr", "vpr" },
        ["ranged"] = new[] { "brd", "mch", "dnc" },
        ["caster"] = new[] { "blm", "smn", "rdm", "pct" },
    };

    public static readonly Dictionary<string, string> RoleOfJob = RoleJobs.SelectMany(r => r.Value.Select(j => (j, r.Key))).ToDictionary(x => x.j, x => x.Key, StringComparer.Ordinal);

    public static readonly string[] FcTags = { "raid", "newbie", "casual", "hardcore", "chill", "events", "crafting", "gathering", "housing", "pvp", "roleplay", "mentor" };

    public static readonly string[] RegionOrder = { "North America (NA)", "Europe (EU)", "Oceania (OCE)", "Japan (JP)" };

    public static readonly Dictionary<string, (string Key, string Short)> RegionInfo = new(StringComparer.Ordinal)
    {
        ["North America (NA)"] = ("na", "NA"),
        ["Europe (EU)"] = ("eu", "EU"),
        ["Oceania (OCE)"] = ("oce", "OCE"),
        ["Japan (JP)"] = ("jp", "JP"),
    };

    /// <summary>Region → Data Center → worlds (the website's list; ordered as on the website).</summary>
    public static readonly Dictionary<string, (string Dc, string[] Worlds)[]> Regions = new(StringComparer.Ordinal)
    {
        ["North America (NA)"] = new[]
        {
            ("Aether", new[] { "Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren" }),
            ("Primal", new[] { "Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros" }),
            ("Crystal", new[] { "Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera" }),
            ("Dynamis", new[] { "Cuchulainn", "Golem", "Halicarnassus", "Kraken", "Maduin", "Marilith", "Rafflesia", "Seraph" }),
        },
        ["Europe (EU)"] = new[]
        {
            ("Chaos", new[] { "Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan" }),
            ("Light", new[] { "Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark" }),
        },
        ["Oceania (OCE)"] = new[]
        {
            ("Materia", new[] { "Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan" }),
        },
        ["Japan (JP)"] = new[]
        {
            ("Elemental", new[] { "Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Tonberry", "Typhon" }),
            ("Gaia", new[] { "Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima" }),
            ("Mana", new[] { "Anima", "Asura", "Chocobo", "Hades", "Ixion", "Masamune", "Pandaemonium", "Titan" }),
            ("Meteor", new[] { "Belias", "Mandragora", "Ramuh", "Shinryu", "Unicorn", "Valefor", "Yojimbo", "Zeromus" }),
        },
    };

    public static IEnumerable<string> DcsOf(string region) =>
        Regions.TryGetValue(region, out var dcs) ? dcs.Select(d => d.Dc) : Enumerable.Empty<string>();

    public static IEnumerable<string> WorldsOf(string region, string dc) =>
        Regions.TryGetValue(region, out var dcs) ? dcs.Where(d => d.Dc == dc).SelectMany(d => d.Worlds) : Enumerable.Empty<string>();

    public static string RegionShort(string region) => RegionInfo.TryGetValue(region, out var r) ? r.Short : region;

    public static string RegionName(Loc loc, string region) => RegionInfo.TryGetValue(region, out var r) ? loc.T("region." + r.Key) : region;

    public static string ContentName(Loc loc, string id) => loc.T("bd.c." + id);

    public static string JobName(Loc loc, string id) => loc.T("job." + id);

    public static string RoleName(Loc loc, string id) => loc.T("bd.role." + id);

    public static string TagName(Loc loc, string id) => loc.T("bd.tag." + id);

    /// <summary>"World · DC (NA)" as on the website.</summary>
    public static string PlaceText(Poster p)
    {
        var parts = new List<string>();
        if (p.World.Length > 0)
            parts.Add(p.World);
        if (p.Dc.Length > 0)
            parts.Add(p.Dc);
        return string.Join(" · ", parts) + (p.Region.Length > 0 ? " (" + RegionShort(p.Region) + ")" : string.Empty);
    }

    /// <summary>"Looking for an FC": the worlds they would join (none = any world of the Data Center).</summary>
    public static string LffcPlace(Loc loc, Poster p)
    {
        var w = p.Worlds.Count > 0 ? string.Join(", ", p.Worlds) : loc.T("bd.p.anyWorld");
        return w + (p.Dc.Length > 0 ? " · " + p.Dc : string.Empty) + (p.Region.Length > 0 ? " (" + RegionShort(p.Region) + ")" : string.Empty);
    }

    public static string Fold(string s) => (s ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLowerInvariant();

    private static bool MatchWords(string query, string hay)
    {
        var words = Fold(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.All(w => hay.Contains(w, StringComparison.Ordinal));
    }

    /// <summary>A poster's start day in the player's own time zone (the website's day filter).</summary>
    public static DateTime LocalDay(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().Date;

    // ---------------- filter states ----------------
    public sealed class StaticFilter
    {
        public string Tab = "lfm";
        public string Query = string.Empty;
        public string Cat = string.Empty;   // "", ult, sav, other
        public string Fight = string.Empty;
        public string Role = string.Empty;
        public string Job = string.Empty;
        public string Region = string.Empty;
        public string Dc = string.Empty;
        public string World = string.Empty;
    }

    public sealed class FcFilter
    {
        public string Tab = "fc";
        public readonly HashSet<string> Tags = new(StringComparer.Ordinal);
        public string Region = string.Empty;
        public string Dc = string.Empty;
        public string World = string.Empty;
    }

    public sealed class EventFilter
    {
        public string Query = string.Empty;
        public DateTime? Day;
        public string Sort = "soon"; // soon, az, za, new, old
        public string Region = string.Empty;
        public string Dc = string.Empty;
    }

    // ---------------- matching (as on the website) ----------------
    public static bool MatchStatic(Loc loc, StaticFilter f, Poster p)
    {
        if (p.Kind != f.Tab)
            return false;
        if (f.Cat == "ult" && !(f.Fight.Length > 0 ? p.Content.Contains(f.Fight) : p.Content.Any(c => Ultimates.Contains(c))))
            return false;
        if (f.Cat == "sav" && !(f.Fight.Length > 0 ? p.Content.Contains(f.Fight) : p.Content.Any(c => Savage.Contains(c))))
            return false;
        if (f.Cat == "other" && !p.Content.Contains("other"))
            return false;
        if (f.Job.Length > 0)
        {
            if (!(p.Jobs.Contains(f.Job) || (RoleOfJob.TryGetValue(f.Job, out var jr) && p.Roles.Contains(jr))))
                return false;
        }
        else if (f.Role.Length > 0 && !(p.Roles.Contains(f.Role) || p.Jobs.Any(j => RoleOfJob.TryGetValue(j, out var r) && r == f.Role)))
        {
            return false;
        }

        // a static without a world is open to the whole Data Center
        if (!MatchPlace(p, f.Region, f.Dc, f.World, true))
            return false;
        if (f.Query.Trim().Length > 0)
        {
            var parts = new List<string> { p.Title, p.Description, p.Schedule, p.OwnerName, p.World, p.Dc, RegionName(loc, p.Region) };
            parts.AddRange(p.Content.Select(c => ContentName(loc, c)));
            parts.AddRange(p.Jobs.Select(j => JobName(loc, j)));
            parts.AddRange(p.Roles.Select(r => RoleName(loc, r)));
            if (!MatchWords(f.Query, Fold(string.Join(" \n ", parts))))
                return false;
        }

        return true;
    }

    public static bool MatchPlace(Poster p, string region, string dc, string world, bool worldOptional)
    {
        if (region.Length > 0 && p.Region != region)
            return false;
        if (dc.Length > 0 && p.Dc != dc)
            return false;
        if (world.Length > 0 && p.World != world && !(worldOptional && p.World.Length == 0))
            return false;
        return true;
    }

    public static bool MatchFc(FcFilter f, Poster p)
    {
        if (p.Kind != f.Tab || !f.Tags.All(t => p.Tags.Contains(t)))
            return false;
        if (p.Kind == "fc")
            return MatchPlace(p, f.Region, f.Dc, f.World, false);
        // a player with no world picked would join any world of the Data Center
        return (f.Region.Length == 0 || p.Region == f.Region) && (f.Dc.Length == 0 || p.Dc == f.Dc) &&
               (f.World.Length == 0 || p.Worlds.Count == 0 || p.Worlds.Contains(f.World));
    }

    public static bool MatchEvent(EventFilter f, Poster p)
    {
        if (p.Kind != "event" || !MatchPlace(p, f.Region, f.Dc, string.Empty, false))
            return false;
        if (f.Day is { } day && LocalDay(p.EventAt) != day)
            return false;
        if (f.Query.Trim().Length > 0 && !MatchWords(f.Query, Fold(p.Title + " \n " + p.OwnerName)))
            return false;
        return true;
    }

    /// <summary>Sorts events. Default "Starting soonest": the Featured top 3 first (#1 = most stars), then soonest to latest.</summary>
    public static List<Poster> SortEvents(IEnumerable<Poster> list, string sort, CultureInfo culture)
    {
        var cmp = culture.CompareInfo;
        const CompareOptions opts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;
        var items = list.ToList();
        Comparison<Poster> soon = (a, b) => a.EventAt.CompareTo(b.EventAt);
        Comparison<Poster> by = sort switch
        {
            "az" => (a, b) => Or(cmp.Compare(a.Title, b.Title, opts), soon(a, b)),
            "za" => (a, b) => Or(cmp.Compare(b.Title, a.Title, opts), soon(a, b)),
            "new" => (a, b) => Or(b.CreatedAt.CompareTo(a.CreatedAt), soon(a, b)),
            "old" => (a, b) => Or(a.CreatedAt.CompareTo(b.CreatedAt), soon(a, b)),
            _ => (a, b) => Or(Rank(a).CompareTo(Rank(b)), soon(a, b)),
        };
        // stable sort (List.Sort is not stable): fall back to the feed order (id order)
        return items.Select((p, i) => (p, i)).OrderBy(x => x, Comparer<(Poster p, int i)>.Create((x, y) => Or(by(x.p, y.p), x.i.CompareTo(y.i)))).Select(x => x.p).ToList();
    }

    private static int Rank(Poster p) => p.Featured is >= 1 and <= 3 ? p.Featured : 99;

    private static int Or(int first, int second) => first != 0 ? first : second;

    /// <summary>The website's poster colour pick for events (FNV-1a hash of "ev" + id).</summary>
    public static int EventTone(long id)
    {
        uint h = 2166136261;
        foreach (var ch in "ev" + id.ToString(CultureInfo.InvariantCulture))
        {
            h ^= ch;
            h *= 16777619;
        }

        return (int)(h % 5);
    }

    public static readonly string[] EventTones = { "pink", "mint", "lemon", "lilac", "sky" };
}
