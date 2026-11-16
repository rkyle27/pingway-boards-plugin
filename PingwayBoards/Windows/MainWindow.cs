using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

namespace PingwayBoards.Windows;

/// <summary>
/// The Pingway Boards window: Static recruitment, FC recruitment and Community events, with the website's filters.
/// Read-only and text only: no sign-up / recruit / invite / star buttons. "View full poster" and poster links open the
/// browser only after a warning pop-up (OK / Cancel). No animations.
/// </summary>
public sealed class MainWindow : Window, IDisposable
{
    private const string PopupId = "###pb-open-browser";

    private readonly Plugin plugin;
    private readonly Boards.StaticFilter fs = new();
    private readonly Boards.FcFilter ff = new();
    private readonly Boards.EventFilter fe = new();

    private string board = "static"; // static, fc, event
    private bool justOpened = true;
    private List<Poster>? demoPosters;

    // the browser warning pop-up
    private string pendingUrl = string.Empty;
    private string pendingMessage = string.Empty;
    private bool openPopupNow;

    // website colours
    private static readonly uint Ink = Ui.Rgba(0x2b, 0x20, 0x13);
    private static readonly uint EventInk = Ui.Rgba(0x2a, 0x22, 0x30);
    private static readonly uint Gold = Ui.Rgba(0xd8, 0xb5, 0x6a);
    private static readonly uint Gold2 = Ui.Rgba(0xf3, 0xdc, 0x9e);

    private static readonly Theme StaticTheme = new()
    {
        Text = Ui.Rgba(0x2b, 0x20, 0x13), Label = Ui.Rgba(0x5b, 0x40, 0x24), Muted = Ui.Rgba(0xff, 0xf3, 0xdc),
        PanelBg = Ui.Rgba(250, 240, 216, 0.9f), PanelBorder = Ui.Rgba(74, 53, 32, 0.25f),
        ChipBg = Ui.Rgba(0xff, 0xfa, 0xf0), ChipText = Ui.Rgba(0x4a, 0x35, 0x20), ChipBorder = Ui.Rgba(74, 53, 32, 0.25f),
        ChipOnBg = Ui.Rgba(0x6b, 0x3f, 0x1d), ChipOnText = Ui.Rgba(0xff, 0xf3, 0xdc),
        FrameBg = Ui.Rgba(0xff, 0xfa, 0xf0), PopupBg = Ui.Rgba(0xfb, 0xf3, 0xe0),
    };

    private static readonly Theme FcTheme = new()
    {
        Text = Ui.Rgba(0xec, 0xe6, 0xd6), Label = Gold, Muted = Ui.Rgba(0xc9, 0xc0, 0xaa),
        PanelBg = Ui.Rgba(255, 255, 255, 0.04f), PanelBorder = Ui.Rgba(216, 181, 106, 0.18f),
        ChipBg = Ui.Rgba(255, 255, 255, 0.05f), ChipText = Ui.Rgba(0xdd, 0xd5, 0xc2), ChipBorder = Ui.Rgba(216, 181, 106, 0.3f),
        ChipOnBg = Ui.Rgba(0xe6, 0xc6, 0x77), ChipOnText = Ui.Rgba(0x1d, 0x16, 0x08),
        FrameBg = Ui.Rgba(255, 255, 255, 0.08f), PopupBg = Ui.Rgba(0x14, 0x1a, 0x2c),
    };

    private static readonly Theme EventTheme = new()
    {
        Text = Ui.Rgba(0xf3, 0xf1, 0xe7), Label = Ui.Rgba(0xe8, 0xf3, 0xe6), Muted = Ui.Rgba(0xe8, 0xf3, 0xe6),
        PanelBg = Ui.Rgba(255, 255, 255, 0.03f), PanelBorder = Ui.Rgba(255, 255, 255, 0.35f),
        ChipBg = Ui.Rgba(255, 255, 255, 0f), ChipText = Ui.Rgba(0xf3, 0xf1, 0xe7), ChipBorder = Ui.Rgba(255, 255, 255, 0.55f),
        ChipOnBg = Ui.Rgba(0xf3, 0xf1, 0xe7), ChipOnText = Ui.Rgba(0x24, 0x49, 0x3a),
        FrameBg = Ui.Rgba(255, 255, 255, 0.10f), PopupBg = Ui.Rgba(0x24, 0x49, 0x3a),
    };

    private static readonly Dictionary<string, uint> RoleColor = new(StringComparer.Ordinal)
    {
        ["tank"] = Ui.Rgba(0x2f, 0x62, 0xb3),
        ["healer"] = Ui.Rgba(0x2e, 0x8a, 0x4c),
        ["melee"] = Ui.Rgba(0xb3, 0x3a, 0x3a),
        ["ranged"] = Ui.Rgba(0xb8, 0x56, 0x2a),
        ["caster"] = Ui.Rgba(0x8a, 0x3f, 0xa8),
    };

    // event invitation band colours (pink, mint, lemon, lilac, sky)
    private static readonly uint[] EventBand =
    {
        Ui.Rgba(0xe8, 0x55, 0x7a), Ui.Rgba(0x2f, 0x9c, 0x6b), Ui.Rgba(0xd6, 0x9a, 0x12), Ui.Rgba(0x7a, 0x4f, 0xd6), Ui.Rgba(0x2f, 0x7f, 0xc9),
    };

    public MainWindow(Plugin plugin)
        : base("Pingway Boards###pingway-boards-main")
    {
        this.plugin = plugin;
        this.Size = new Vector2(1000, 780);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 440),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    private Loc L => this.plugin.Loc;

    public void Dispose()
    {
    }

    public override void OnOpen() => this.justOpened = true;

    /// <summary>The demo switch changed: show the right posters (and read the feed right away when the demo was switched off).</summary>
    public void RequestRefresh() => this.justOpened = true;

    public override void Draw()
    {
        var s = Ui.S;
        IReadOnlyList<Poster> posters;
        FeedSnapshot? snap = null;
        if (this.plugin.Configuration.DemoMode)
        {
            this.demoPosters ??= DemoData.Load();
            posters = this.demoPosters;
        }
        else
        {
            // the only network use: the public feed, at most every 2 minutes while this window is open
            this.plugin.Feed.Tick(this.justOpened);
            snap = this.plugin.Feed.Current;
            posters = snap.Posters;
        }

        this.justOpened = false;

        this.DrawTopBar();
        this.DrawStatus(snap);
        ImGui.Spacing();
        this.DrawBoard(posters);
        this.DrawOpenPopup(s);
    }

    // ------------------------------------------------------------------ top bar + status
    private void DrawTopBar()
    {
        var s = Ui.S;
        var items = new[] { ("static", "bd.static.title"), ("fc", "bd.fc.title"), ("event", "bd.ev.title") };
        var first = true;
        foreach (var (key, label) in items)
        {
            if (!first)
                ImGui.SameLine();
            first = false;
            var on = this.board == key;
            var accent = Ui.Rgba(0x7c, 0x4d, 0xdb);
            ImGui.PushStyleColor(ImGuiCol.Button, on ? accent : Ui.Rgba(255, 255, 255, 0.06f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, on ? accent : Ui.Rgba(124, 77, 219, 0.45f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, accent);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f * s);
            if (ImGui.Button(this.L.T(label) + "##pb-board-" + key))
                this.board = key;
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
        }

        var settings = this.L.T("pb.settings.btn");
        var w = ImGui.CalcTextSize(settings).X + (ImGui.GetStyle().FramePadding.X * 2f);
        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > w)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);
        if (ImGui.Button(settings + "##pb-settings"))
            this.plugin.OpenConfigUi();
    }

    private void DrawStatus(FeedSnapshot? snap)
    {
        if (snap == null)
        {
            ImGui.TextColored(new Vector4(0.95f, 0.8f, 0.4f, 1f), this.L.T("pb.demoBadge"));
            return;
        }

        if (snap.Error != null)
        {
            var msg = snap.Error switch
            {
                "rate_limited" => this.L.T("pb.err.rate_limited", ("s", Math.Max(1, snap.RetryAfterSeconds).ToString(this.L.Culture))),
                "not_found" => this.L.T("pb.err.not_found"),
                "method" => this.L.T("pb.err.method"),
                "network" => this.L.T("pb.err.network"),
                "update" => this.L.T("pb.err.update"),
                _ => this.L.T("pb.err.error"),
            };
            if (snap.HasData)
                msg += " " + this.L.T("pb.err.showingOld");
            ImGui.PushStyleColor(ImGuiCol.Text, Ui.Rgba(0xff, 0x9b, 0x8f));
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(msg);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
            return;
        }

        if (!snap.HasData)
        {
            ImGui.TextDisabled(this.L.T("pb.loading"));
            return;
        }

        ImGui.TextDisabled(this.L.T("pb.updated", ("time", snap.LoadedAt.ToLocalTime().ToString("t", this.L.Culture))));
    }

    // ------------------------------------------------------------------ the board (background image + scrolling content)
    private void DrawBoard(IReadOnlyList<Poster> posters)
    {
        var s = Ui.S;
        var min = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        if (avail.X < 100f || avail.Y < 100f)
            return;
        var max = min + avail;
        var dl = ImGui.GetWindowDrawList();
        var file = this.board switch { "fc" => "board_fc.png", "event" => "board_event.png", _ => "board_static.png" };
        var tex = this.plugin.Assets.Get(file);
        if (tex != null)
            Ui.DrawSliced(dl, tex, min, max, 40f, 40f, 40f, 40f, s);
        else
            dl.AddRectFilled(min, max, this.board switch { "fc" => Ui.Rgba(0x14, 0x1a, 0x2c), "event" => Ui.Rgba(0x2f, 0x5a, 0x44), _ => Ui.Rgba(0xb9, 0x85, 0x52) }, 14f * s);

        // the wooden frame / gold edge stays outside the scrolling area (the chalkboard also keeps its chalk tray)
        var marginTop = 20f * s;
        var marginSide = 22f * s;
        var marginBottom = this.board == "event" ? 30f * s : 20f * s;
        ImGui.SetCursorScreenPos(min + new Vector2(marginSide, marginTop));
        var size = new Vector2(avail.X - (2f * marginSide), avail.Y - marginTop - marginBottom);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, 0u);
        if (ImGui.BeginChild("##pb-board-" + this.board, size, false))
        {
            ImGui.Dummy(new Vector2(1f, 4f * s));
            switch (this.board)
            {
                case "fc":
                    this.DrawFcBoard(posters);
                    break;
                case "event":
                    this.DrawEventBoard(posters);
                    break;
                default:
                    this.DrawStaticBoard(posters);
                    break;
            }

            ImGui.Dummy(new Vector2(1f, 10f * s));
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    // ------------------------------------------------------------------ Static recruitment
    private void DrawStaticBoard(IReadOnlyList<Poster> posters)
    {
        var s = Ui.S;
        var dl = ImGui.GetWindowDrawList();
        var title = this.L.T("bd.static.title");

        // the wooden sign with the board name
        ImGui.SetWindowFontScale(1.25f);
        var tsize = ImGui.CalcTextSize(title);
        var avail = ImGui.GetContentRegionAvail().X;
        var signW = Math.Min(avail, Math.Max(Assets.SignW * s, tsize.X + (90f * s)));
        var signH = tsize.Y + (16f * s);
        var start = ImGui.GetCursorScreenPos();
        var signMin = new Vector2(start.X + ((avail - signW) / 2f), start.Y);
        var sign = this.plugin.Assets.Get("sign_static.png");
        if (sign != null)
            Ui.DrawSliced(dl, sign, signMin, signMin + new Vector2(signW, signH), 30f, 0f, 30f, 0f, s);
        else
            dl.AddRectFilled(signMin, signMin + new Vector2(signW, signH), Ui.Rgba(0xd9, 0xa9, 0x6a), 6f * s);
        ImGui.SetCursorScreenPos(new Vector2(signMin.X + ((signW - tsize.X) / 2f), signMin.Y + ((signH - tsize.Y) / 2f)));
        ImGui.TextColored(Ui.Rgba(0x3a, 0x22, 0x10), title);
        ImGui.SetWindowFontScale(1f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, signMin.Y + signH + (12f * s)));

        // the two paper tabs
        this.Tabs(new[] { ("lfm", this.L.T("bd.static.lfm"), Ui.Rgba(0xf1, 0xdf, 0xb3)), ("lfs", this.L.T("bd.static.lfs"), Ui.Rgba(0xcf, 0xe1, 0xdf)) },
            this.fs.Tab, Ui.Rgba(0x3a, 0x2a, 0x16), Ui.Rgba(0x3a, 0x2a, 0x16), true, out var picked);
        if (picked != null)
            this.fs.Tab = picked;
        ImGui.Dummy(new Vector2(1f, 6f * s));

        this.Panel(StaticTheme, innerW => this.StaticFilters(innerW));

        var list = posters.Where(p => Boards.MatchStatic(this.L, this.fs, p)).ToList();
        this.CountLine(StaticTheme, list.Count);
        this.Grid(list, StaticTheme);
    }

    private void StaticFilters(float innerW)
    {
        var s = Ui.S;
        var t = StaticTheme;
        var f = this.fs;
        var labelW = this.LabelWidth("bd.f.content", "bd.f.role", "bd.f.job", "bd.f.region", "bd.f.dc", "bd.f.world");

        ImGui.SetNextItemWidth(Math.Min(420f * s, innerW));
        ImGui.InputTextWithHint("##pb-q-static", this.L.T("bd.f.searchPh"), ref f.Query, 60);

        var row = new Ui.ChipRow(t, this.L.T("bd.f.content"), labelW, innerW);
        if (row.Chip(this.L.T("bd.f.any"), f.Cat.Length == 0, "cat-any")) { f.Cat = string.Empty; f.Fight = string.Empty; }
        if (row.Chip(this.L.T("bd.f.ult"), f.Cat == "ult", "cat-ult")) { f.Cat = "ult"; f.Fight = string.Empty; }
        if (row.Chip(this.L.T("bd.f.sav"), f.Cat == "sav", "cat-sav")) { f.Cat = "sav"; f.Fight = string.Empty; }
        if (row.Chip(this.L.T("bd.c.other"), f.Cat == "other", "cat-other")) { f.Cat = "other"; f.Fight = string.Empty; }

        if (f.Cat is "ult" or "sav")
        {
            var sub = new Ui.ChipRow(t, string.Empty, labelW, innerW);
            if (sub.Chip(this.L.T(f.Cat == "ult" ? "bd.f.allUlt" : "bd.f.allSav"), f.Fight.Length == 0, "fight-any"))
                f.Fight = string.Empty;
            foreach (var c in f.Cat == "ult" ? Boards.Ultimates : Boards.Savage)
            {
                if (sub.Chip(Boards.ContentName(this.L, c), f.Fight == c, "fight-" + c))
                    f.Fight = c;
            }
        }

        var roles = new Ui.ChipRow(t, this.L.T("bd.f.role"), labelW, innerW);
        if (roles.Chip(this.L.T("bd.f.any"), f.Role.Length == 0, "role-any")) { f.Role = string.Empty; f.Job = string.Empty; }
        foreach (var r in Boards.RoleOrder)
        {
            if (roles.Chip(Boards.RoleName(this.L, r), f.Role == r, "role-" + r, RoleColor[r]))
            {
                f.Role = r;
                f.Job = string.Empty;
            }
        }

        if (f.Role.Length > 0)
        {
            var jobs = new Ui.ChipRow(t, this.L.T("bd.f.job"), labelW, innerW);
            if (jobs.Chip(this.L.T("bd.f.anyJob"), f.Job.Length == 0, "job-any"))
                f.Job = string.Empty;
            foreach (var j in Boards.RoleJobs[f.Role])
            {
                if (jobs.Chip(Boards.JobName(this.L, j), f.Job == j, "job-" + j))
                    f.Job = j;
            }
        }

        this.PlaceFilters(t, labelW, innerW, ref f.Region, ref f.Dc, ref f.World, true);
    }

    // ------------------------------------------------------------------ FC recruitment
    private void DrawFcBoard(IReadOnlyList<Poster> posters)
    {
        var s = Ui.S;
        var dl = ImGui.GetWindowDrawList();
        var title = this.L.T("bd.fc.title");

        // gold title between two thin gold lines
        ImGui.SetWindowFontScale(1.3f);
        var tsize = ImGui.CalcTextSize(title);
        var avail = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();
        var tx = start.X + Math.Max(0f, (avail - tsize.X) / 2f);
        var midY = start.Y + (tsize.Y / 2f);
        var line = Math.Min(110f * s, Math.Max(0f, ((avail - tsize.X) / 2f) - (14f * s)));
        if (line > 10f * s)
        {
            dl.AddLine(new Vector2(tx - (12f * s) - line, midY), new Vector2(tx - (12f * s), midY), Gold, 1f);
            dl.AddLine(new Vector2(tx + tsize.X + (12f * s), midY), new Vector2(tx + tsize.X + (12f * s) + line, midY), Gold, 1f);
        }

        ImGui.SetCursorScreenPos(new Vector2(tx, start.Y));
        ImGui.TextColored(Gold2, title);
        ImGui.SetWindowFontScale(1f);
        ImGui.Dummy(new Vector2(1f, 2f * s));
        Ui.CenteredText(this.L.T(this.ff.Tab == "lffc" ? "bd.fc.subLffc" : "bd.fc.sub"), Ui.Rgba(0xb9, 0xb2, 0xa0));
        ImGui.Dummy(new Vector2(1f, 6f * s));

        this.Tabs(new[] { ("fc", this.L.T("bd.fc.fc"), Gold), ("lffc", this.L.T("bd.fc.lffc"), Gold) },
            this.ff.Tab, Ui.Rgba(0x1d, 0x16, 0x08), Gold2, false, out var picked);
        if (picked != null)
            this.ff.Tab = picked;
        ImGui.Dummy(new Vector2(1f, 6f * s));

        this.Panel(FcTheme, innerW =>
        {
            var labelW = this.LabelWidth("bd.f.tags", "bd.f.region", "bd.f.dc", "bd.f.world");
            var tags = new Ui.ChipRow(FcTheme, this.L.T("bd.f.tags"), labelW, innerW);
            foreach (var g in Boards.FcTags)
            {
                if (tags.Chip(Boards.TagName(this.L, g), this.ff.Tags.Contains(g), "tag-" + g))
                {
                    if (!this.ff.Tags.Remove(g))
                        this.ff.Tags.Add(g);
                }
            }

            this.PlaceFilters(FcTheme, labelW, innerW, ref this.ff.Region, ref this.ff.Dc, ref this.ff.World, true);
        });

        var list = posters.Where(p => Boards.MatchFc(this.ff, p)).ToList();
        this.CountLine(FcTheme, list.Count);
        this.Grid(list, FcTheme);
    }

    // ------------------------------------------------------------------ Community events
    private void DrawEventBoard(IReadOnlyList<Poster> posters)
    {
        var s = Ui.S;
        ImGui.Dummy(new Vector2(1f, 62f * s)); // room for the chalk doodles of the board picture
        ImGui.SetWindowFontScale(1.5f);
        Ui.CenteredText(this.L.T("bd.ev.title"), Ui.Rgba(0xfd, 0xfb, 0xf1));
        ImGui.SetWindowFontScale(1f);
        Ui.CenteredText(this.L.T("bd.ev.sub"), Ui.Rgba(0xd7, 0xe6, 0xd9));
        ImGui.Dummy(new Vector2(1f, 8f * s));

        var events = posters.Where(p => p.Kind == "event").ToList();
        this.Panel(EventTheme, innerW => this.EventFilters(innerW, events));

        var list = Boards.SortEvents(events.Where(p => Boards.MatchEvent(this.fe, p)), this.fe.Sort, this.L.Culture);
        this.CountLine(EventTheme, list.Count);
        this.Grid(list, EventTheme);
    }

    private void EventFilters(float innerW, List<Poster> events)
    {
        var s = Ui.S;
        var t = EventTheme;
        var f = this.fe;
        var labelW = this.LabelWidth("bd.f.day", "bd.f.sort", "bd.f.region", "bd.f.dc");

        ImGui.SetNextItemWidth(Math.Min(420f * s, innerW));
        ImGui.InputTextWithHint("##pb-q-event", this.L.T("bd.f.evSearchPh"), ref f.Query, 60);

        // Day: the days that have events, in your own time zone
        var x0 = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.PushStyleColor(ImGuiCol.Text, t.Label);
        ImGui.TextUnformatted(this.L.T("bd.f.day"));
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.SetCursorPosX(x0 + labelW);
        var days = events.Select(p => Boards.LocalDay(p.EventAt)).Distinct().OrderBy(d => d).ToList();
        var preview = f.Day is { } sel ? this.DayText(sel) : this.L.T("pb.f.allDays");
        ImGui.SetNextItemWidth(Math.Min(240f * s, Math.Max(80f * s, innerW - labelW)));
        if (ImGui.BeginCombo("##pb-day", preview))
        {
            if (ImGui.Selectable(this.L.T("pb.f.allDays") + "##day-all", f.Day == null))
                f.Day = null;
            for (var i = 0; i < days.Count; i++)
            {
                if (ImGui.Selectable(this.DayText(days[i]) + "##day-" + i.ToString(this.L.Culture), f.Day == days[i]))
                    f.Day = days[i];
            }

            ImGui.EndCombo();
        }

        var sort = new Ui.ChipRow(t, this.L.T("bd.f.sort"), labelW, innerW);
        foreach (var k in new[] { "soon", "az", "za", "new", "old" })
        {
            if (sort.Chip(this.L.T("bd.f.sort." + k), f.Sort == k, "sort-" + k))
                f.Sort = k;
        }

        var none = string.Empty;
        this.PlaceFilters(t, labelW, innerW, ref f.Region, ref f.Dc, ref none, false);
    }

    // ------------------------------------------------------------------ shared filter parts
    private void PlaceFilters(Theme t, float labelW, float innerW, ref string region, ref string dc, ref string world, bool withWorld)
    {
        var regions = new Ui.ChipRow(t, this.L.T("bd.f.region"), labelW, innerW);
        if (regions.Chip(this.L.T("bd.f.any"), region.Length == 0, "region-any"))
        {
            region = string.Empty;
            dc = string.Empty;
            world = string.Empty;
        }

        foreach (var r in Boards.RegionOrder)
        {
            if (regions.Chip(Boards.RegionShort(r), region == r, "region-" + Boards.RegionShort(r)))
            {
                region = r;
                dc = string.Empty;
                world = string.Empty;
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Boards.RegionName(this.L, r));
        }

        if (region.Length == 0)
            return;
        var dcs = new Ui.ChipRow(t, this.L.T("bd.f.dc"), labelW, innerW);
        if (dcs.Chip(this.L.T("bd.f.any"), dc.Length == 0, "dc-any"))
        {
            dc = string.Empty;
            world = string.Empty;
        }

        foreach (var d in Boards.DcsOf(region))
        {
            if (dcs.Chip(d, dc == d, "dc-" + d))
            {
                dc = d;
                world = string.Empty;
            }
        }

        if (!withWorld || dc.Length == 0)
            return;
        var worlds = new Ui.ChipRow(t, this.L.T("bd.f.world"), labelW, innerW);
        if (worlds.Chip(this.L.T("bd.f.any"), world.Length == 0, "world-any"))
            world = string.Empty;
        foreach (var w in Boards.WorldsOf(region, dc))
        {
            if (worlds.Chip(w, world == w, "world-" + w))
                world = w;
        }
    }

    private float LabelWidth(params string[] keys)
    {
        var w = 0f;
        foreach (var k in keys)
            w = Math.Max(w, ImGui.CalcTextSize(this.L.T(k)).X);
        return w + (12f * Ui.S);
    }

    /// <summary>The filter panel: a paper strip / glass / chalk outline behind the filter rows.</summary>
    private void Panel(Theme t, Action<float> content)
    {
        var s = Ui.S;
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        var pad = new Vector2(12f * s, 10f * s);
        var innerW = Math.Max(50f, w - (2f * pad.X));

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(start + pad);
        ImGui.BeginGroup();
        ImGui.PushStyleColor(ImGuiCol.Text, t.Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Ui.Mix(t.Text, t.FrameBg, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, t.FrameBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, t.FrameBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, t.FrameBg);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, t.PopupBg);
        ImGui.PushStyleColor(ImGuiCol.Header, t.ChipOnBg);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Ui.Mix(t.PopupBg, t.ChipOnBg, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, t.ChipOnBg);
        ImGui.PushStyleColor(ImGuiCol.Border, t.ChipBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        content(innerW);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(10);
        ImGui.EndGroup();
        var end = new Vector2(start.X + w, ImGui.GetItemRectMax().Y + pad.Y);

        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(start, end, t.PanelBg, 8f * s);
        dl.AddRect(start, end, t.PanelBorder, 8f * s);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(start.X, end.Y + (6f * s)));
        ImGui.Dummy(new Vector2(w, 1f));
    }

    private void Tabs((string Key, string Label, uint Color)[] tabs, string current, uint onText, uint offText, bool paper, out string? picked)
    {
        var s = Ui.S;
        picked = null;
        var pad = ImGui.GetStyle().FramePadding.X * 2f;
        var spacing = 10f * s;
        var total = tabs.Sum(t => ImGui.CalcTextSize(t.Label).X + pad) + (spacing * (tabs.Length - 1));
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - total) / 2f));
        for (var i = 0; i < tabs.Length; i++)
        {
            var (key, label, color) = tabs[i];
            if (i > 0)
                ImGui.SameLine(0f, spacing);
            var on = current == key;
            uint bg;
            uint fg;
            if (paper)
            {
                // the website's paper tabs: the chosen one fully opaque, the other faded
                bg = on ? color : Ui.Mix(color, Ui.Rgba(0x8a, 0x5c, 0x30), 0.35f);
                fg = onText;
            }
            else
            {
                bg = on ? color : Ui.Rgba(255, 255, 255, 0.04f);
                fg = on ? onText : offText;
            }

            ImGui.PushStyleColor(ImGuiCol.Button, bg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, on ? bg : Ui.Mix(bg, color, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
            ImGui.PushStyleColor(ImGuiCol.Text, fg);
            ImGui.PushStyleColor(ImGuiCol.Border, paper ? Ui.Rgba(0, 0, 0, 0.25f) : Ui.Rgba(216, 181, 106, 0.4f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, (paper ? 3f : 12f) * s);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button(label + "##pb-tab-" + key))
                picked = key;
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(5);
        }
    }

    private void CountLine(Theme t, int n)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, t.Muted);
        ImGui.TextUnformatted(this.L.Count("bd.count", n));
        ImGui.PopStyleColor();
    }

    // ------------------------------------------------------------------ the posters
    private void Grid(List<Poster> items, Theme t)
    {
        var s = Ui.S;
        if (items.Count == 0)
        {
            ImGui.Dummy(new Vector2(1f, 20f * s));
            Ui.CenteredText(this.L.T("bd.empty"), t.Muted);
            return;
        }

        var cardW = Assets.PosterCardW * s;
        var gap = 22f * s;
        var avail = ImGui.GetContentRegionAvail().X;
        var cols = Math.Max(1, (int)((avail + gap) / (cardW + gap)));
        if (cols == 1 && avail < cardW)
            cardW = Math.Max(160f * s, avail);
        var totalW = (cols * cardW) + ((cols - 1) * gap);
        var origin = ImGui.GetCursorScreenPos();
        var x0 = origin.X + Math.Max(0f, (avail - totalW) / 2f);
        var y = origin.Y + (16f * s); // room for pins, tape and rolls above the cards

        for (var i = 0; i < items.Count; i += cols)
        {
            var rowH = 0f;
            for (var c = 0; c < cols && i + c < items.Count; c++)
            {
                var h = this.Card(items[i + c], new Vector2(x0 + (c * (cardW + gap)), y), cardW);
                rowH = Math.Max(rowH, h);
            }

            y += rowH + gap + (14f * s);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        ImGui.Dummy(new Vector2(1f, 1f));
    }

    /// <summary>Draws one poster (text over its blank poster picture) and returns its height.</summary>
    private float Card(Poster p, Vector2 pos, float w)
    {
        var s = Ui.S;
        var k = w / Assets.PosterCardW;
        var dl = ImGui.GetWindowDrawList();
        var (padX, padTop, padBottom) = p.Kind switch
        {
            "lfm" => (20f, 36f, 32f),
            "lfs" => (20f, 26f, 20f),
            "event" => (18f, 18f, 18f),
            _ => (16f, 16f, 16f),
        };
        padX *= k;
        padTop *= k;
        padBottom *= k;
        var innerW = w - (2f * padX);

        ImGui.PushID("pb-poster-" + p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(pos + new Vector2(padX, padTop));
        ImGui.BeginGroup();
        var wrapX = ImGui.GetCursorPosX() + innerW;
        ImGui.PushTextWrapPos(wrapX);
        switch (p.Kind)
        {
            case "lfm":
            case "lfs":
                this.StaticCard(p, innerW, wrapX);
                break;
            case "fc":
            case "lffc":
                this.FcCard(p, innerW, wrapX);
                break;
            default:
                this.EventCard(p, innerW, wrapX);
                break;
        }

        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        var bottom = ImGui.GetItemRectMax().Y + padBottom;
        var h = Math.Max(bottom - pos.Y, 180f * k);
        var max = new Vector2(pos.X + w, pos.Y + h);

        dl.ChannelsSetCurrent(0);
        var file = p.Kind switch
        {
            "lfm" => "poster_lfm.png",
            "lfs" => "poster_lfs.png",
            "fc" or "lffc" => "poster_fc.png",
            _ => "poster_event_" + Boards.EventTones[Boards.EventTone(p.Id)] + ".png",
        };
        var tex = this.plugin.Assets.Get(file);
        var imgMin = pos - (new Vector2(Assets.PosterCardX, Assets.PosterCardY) * k);
        var imgMax = max + (new Vector2(Assets.PosterImageW - Assets.PosterCardX - Assets.PosterCardW, Assets.PosterImageH - Assets.PosterCardY - Assets.PosterCardH) * k);
        if (tex != null)
        {
            Ui.DrawSliced(dl, tex, imgMin, imgMax, 20f, Assets.PosterCardY + 60f, 20f, (Assets.PosterImageH - Assets.PosterCardY - Assets.PosterCardH) + 60f, k);
        }
        else
        {
            var fill = p.Kind switch
            {
                "lfm" => Ui.Rgba(0xf1, 0xdf, 0xb3),
                "lfs" => Ui.Rgba(0xcf, 0xe1, 0xdf),
                "fc" or "lffc" => Ui.Rgba(255, 255, 255, 0.07f),
                _ => Ui.Rgba(0xff, 0xe3, 0xea),
            };
            dl.AddRectFilled(pos, max, fill, 8f * s);
        }

        if (p.Kind == "event" && p.Featured is >= 1 and <= 3)
            dl.AddRect(pos - new Vector2(1.5f * s, 1.5f * s), max + new Vector2(1.5f * s, 1.5f * s), Ui.Rgba(0xf2, 0xb9, 0x2e), 10f * s, ImDrawFlags.None, 3f * s);

        dl.ChannelsMerge();
        ImGui.PopID();
        return h;
    }

    private void StaticCard(Poster p, float innerW, float wrapX)
    {
        var s = Ui.S;
        var lfm = p.Kind == "lfm";
        var kickColor = lfm ? Ui.Rgba(0x8a, 0x2f, 0x1f) : Ui.Rgba(0x1f, 0x55, 0x60);
        var faded = Ui.Rgba(0x2b, 0x20, 0x13, 0.72f);
        ImGui.PushStyleColor(ImGuiCol.Text, Ink);

        CenterLine(this.L.T(lfm ? "bd.p.kickLfm" : "bd.p.kickLfs").ToUpper(this.L.Culture), kickColor, innerW);
        Title(p.Title, Ink);

        if (p.Content.Count > 0)
        {
            Rule(innerW, Ui.Rgba(43, 32, 19, 0.35f));
            foreach (var c in p.Content)
                ImGui.TextUnformatted("- " + Boards.ContentName(this.L, c));
            Rule(innerW, Ui.Rgba(43, 32, 19, 0.35f));
        }

        if (p.Roles.Count + p.Jobs.Count > 0)
        {
            ImGui.TextColored(faded, this.L.T(lfm ? "bd.p.wanted" : "bd.p.plays").ToUpper(this.L.Culture));
            var first = true;
            var white = Ui.Rgba(255, 255, 255);
            foreach (var r in p.Roles)
                Ui.Tag(Boards.RoleName(this.L, r), RoleColor.GetValueOrDefault(r, Ink), white, 0u, wrapX, ref first);
            foreach (var j in p.Jobs)
            {
                var role = Boards.RoleOfJob.GetValueOrDefault(j, string.Empty);
                Ui.Tag(Boards.JobName(this.L, j), RoleColor.GetValueOrDefault(role, Ink), white, 0u, wrapX, ref first);
            }
        }

        ImGui.Dummy(new Vector2(1f, 2f * s));
        ImGui.TextUnformatted(this.L.T("bd.p.where") + ": " + Boards.PlaceText(p));
        if (p.Schedule.Length > 0)
            ImGui.TextUnformatted(this.L.T("bd.p.when") + ": " + p.Schedule);
        this.Body(p, Ink, Ui.Rgba(0x3a, 0x2a, 0x16), faded, Ink);
        ImGui.PopStyleColor();
    }

    private void FcCard(Poster p, float innerW, float wrapX)
    {
        var s = Ui.S;
        var text = Ui.Rgba(0xec, 0xe6, 0xd6);
        var desc = Ui.Rgba(0xcf, 0xc8, 0xb6);
        ImGui.PushStyleColor(ImGuiCol.Text, desc);

        ImGui.TextColored(Gold, (p.Kind == "fc" ? Boards.PlaceText(p) : Boards.LffcPlace(this.L, p)).ToUpper(this.L.Culture));
        Title(p.Title, Ui.Rgba(0xff, 0xf7, 0xe2));

        if (p.Tags.Count > 0)
        {
            if (p.Kind == "lffc")
                ImGui.TextColored(Gold, this.L.T("bd.p.lookingFor").ToUpper(this.L.Culture));
            var first = true;
            foreach (var g in p.Tags)
                Ui.Tag(Boards.TagName(this.L, g), Ui.Rgba(216, 181, 106, 0.08f), Gold2, Ui.Rgba(216, 181, 106, 0.45f), wrapX, ref first);
            ImGui.Dummy(new Vector2(1f, 2f * s));
        }

        if (p.Kind == "lffc" && p.Playtime.Length > 0)
            ImGui.TextUnformatted(this.L.T("bd.p.playtime") + ": " + p.Playtime);
        this.Body(p, desc, Gold2, Ui.Rgba(0xa9, 0xa1, 0x8e), text);
        ImGui.PopStyleColor();
    }

    private void EventCard(Poster p, float innerW, float wrapX)
    {
        var s = Ui.S;
        var band = EventBand[Boards.EventTone(p.Id)];
        ImGui.PushStyleColor(ImGuiCol.Text, EventInk);

        if (p.Featured is >= 1 and <= 3)
        {
            var first = true;
            Ui.Tag(this.L.T("bd.featured") + " #" + p.Featured.ToString(this.L.Culture), Ui.Rgba(0xf2, 0xb9, 0x2e), Ui.Rgba(0x3a, 0x2a, 0x06), 0u, wrapX, ref first);
        }

        // the coloured band: "You're invited!"
        var kick = this.L.T("bd.p.invited");
        var dl = ImGui.GetWindowDrawList();
        var at = ImGui.GetCursorScreenPos();
        var ksize = ImGui.CalcTextSize(kick, false, innerW - (8f * s));
        var bandMax = at + new Vector2(innerW, ksize.Y + (8f * s));
        dl.AddRectFilled(at, bandMax, band, 6f * s);
        ImGui.SetCursorScreenPos(at + new Vector2(Math.Max(4f * s, (innerW - ksize.X) / 2f), 4f * s));
        ImGui.PushStyleColor(ImGuiCol.Text, Ui.Rgba(255, 255, 255));
        ImGui.TextUnformatted(kick);
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new Vector2(at.X, bandMax.Y + (6f * s)));

        Title(p.Title, EventInk);
        if (p.EventAt > 0)
            CenterLine(this.WhenText(p.EventAt), Ui.Mix(band, Ui.Rgba(0, 0, 0), 0.25f), innerW);
        ImGui.TextUnformatted(this.L.T("bd.p.where") + ": " + Boards.PlaceText(p));
        ImGui.TextUnformatted(this.L.T("bd.p.hostedBy", ("name", p.Host.Length > 0 ? p.Host : p.OwnerName)));
        this.Body(p, EventInk, Ui.Rgba(0x3a, 0x2a, 0x2e), Ui.Rgba(0x2a, 0x22, 0x30, 0.7f), EventInk, showBy: false);
        ImGui.PopStyleColor();
    }

    /// <summary>Description, links (as text, warning before opening), "by name" and "View full poster".</summary>
    private void Body(Poster p, uint descColor, uint linkColor, uint mutedColor, uint buttonInk, bool showBy = true)
    {
        var s = Ui.S;
        if (p.Description.Length > 0)
        {
            ImGui.Dummy(new Vector2(1f, 2f * s));
            ImGui.PushStyleColor(ImGuiCol.Text, descColor);
            ImGui.TextUnformatted(p.Description);
            ImGui.PopStyleColor();
        }

        if (p.Links.Count > 0)
        {
            ImGui.Dummy(new Vector2(1f, 2f * s));
            ImGui.TextColored(mutedColor, this.L.T("bd.p.links"));
            var n = 0;
            foreach (var link in p.Links)
            {
                var url = UrlSafety.SafeExternal(link.Url);
                if (url == null)
                    continue;
                var domain = UrlSafety.Domain(url);
                var label = (link.Title.Length > 0 ? link.Title : domain) + " (" + domain + ")";
                ImGui.PushID("link-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture));
                n++;
                ImGui.PushStyleColor(ImGuiCol.Text, linkColor);
                ImGui.TextUnformatted("> " + label);
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip(url);
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    this.AskOpen(url, this.L.T("pb.open.link", ("site", domain)));
                ImGui.PopID();
            }
        }

        if (showBy && p.OwnerName.Length > 0)
        {
            ImGui.Dummy(new Vector2(1f, 2f * s));
            ImGui.TextColored(mutedColor, this.L.T("bd.p.by", ("name", p.OwnerName)));
        }

        if (UrlSafety.IsPingway(p.Url))
        {
            ImGui.Dummy(new Vector2(1f, 4f * s));
            ImGui.PushStyleColor(ImGuiCol.Button, Ui.Mix(buttonInk, 0u, 0.92f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Ui.Mix(buttonInk, 0u, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Ui.Mix(buttonInk, 0u, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.Text, buttonInk);
            ImGui.PushStyleColor(ImGuiCol.Border, Ui.Mix(buttonInk, 0u, 0.5f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f * s);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button(this.L.T("pb.view") + "##view"))
                this.AskOpen(p.Url, this.L.T("pb.open.pingway"));
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(5);
        }
    }

    private static void Title(string title, uint color)
    {
        ImGui.SetWindowFontScale(1.18f);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1f);
    }

    private static void CenterLine(string text, uint color, float innerW)
    {
        var w = ImGui.CalcTextSize(text).X;
        if (w < innerW)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((innerW - w) / 2f));
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private static void Rule(float innerW, uint color)
    {
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(new Vector2(p.X, p.Y + 1f), new Vector2(p.X + innerW, p.Y + 1f), color, 1f);
        ImGui.Dummy(new Vector2(innerW, 3f * Ui.S));
    }

    // ------------------------------------------------------------------ dates (in your own time zone)
    private string DayText(DateTime day) => day.ToString(this.L.Code switch
    {
        "ja" => "M月d日(ddd)",
        "de" => "ddd, d. MMM",
        "fr" => "ddd d MMM",
        _ => "ddd, MMM d",
    }, this.L.Culture);

    private string WhenText(long ms)
    {
        var local = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
        return this.DayText(local) + " " + local.ToString("t", this.L.Culture);
    }

    // ------------------------------------------------------------------ the browser warning
    private void AskOpen(string url, string message)
    {
        this.pendingUrl = url;
        this.pendingMessage = message;
        this.openPopupNow = true;
    }

    private void DrawOpenPopup(float s)
    {
        var name = this.L.T("pb.open.title") + PopupId;
        if (this.openPopupNow)
        {
            ImGui.OpenPopup(name);
            this.openPopupNow = false;
        }

        if (!ImGui.BeginPopupModal(name, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + (380f * s));
        ImGui.TextUnformatted(this.pendingMessage);
        ImGui.TextDisabled(this.pendingUrl);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        if (ImGui.Button(this.L.T("pb.ok") + "##pb-open-ok", new Vector2(110f * s, 0f)))
        {
            // only pingway.app pages for "View full poster", https links for poster links; nothing else is ever opened
            if (UrlSafety.IsPingway(this.pendingUrl) || UrlSafety.SafeExternal(this.pendingUrl) == this.pendingUrl)
                Util.OpenLink(this.pendingUrl);
            this.pendingUrl = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(this.L.T("pb.cancel") + "##pb-open-cancel", new Vector2(110f * s, 0f)))
        {
            this.pendingUrl = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }
}
