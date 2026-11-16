using System;
using Dalamud.Configuration;

namespace PingwayBoards;

/// <summary>The plugin settings (saved by Dalamud in its own config folder).</summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>"Demo: sample posters": show the built-in made-up posters (no network) instead of the real feed.</summary>
    public bool DemoMode { get; set; } = false;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
