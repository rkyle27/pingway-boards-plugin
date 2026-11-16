using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace PingwayBoards.Windows;

/// <summary>Settings: the "Demo: sample posters" switch.</summary>
public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base(plugin.Loc.T("pb.settings.title") + "###pingway-boards-settings", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var loc = this.plugin.Loc;
        var s = ImGuiHelpers.GlobalScale;
        var demo = this.plugin.Configuration.DemoMode;
        if (ImGui.Checkbox(loc.T("pb.settings.demo") + "##pb-demo", ref demo))
        {
            this.plugin.Configuration.DemoMode = demo;
            this.plugin.Configuration.Save();
            this.plugin.DemoModeChanged();
        }

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + (380f * s));
        ImGui.TextDisabled(loc.T("pb.settings.demoHint"));
        ImGui.Dummy(new Vector2(1f, 6f * s));
        ImGui.TextUnformatted(loc.T("pb.settings.readonly"));
        ImGui.PopTextWrapPos();
    }
}
