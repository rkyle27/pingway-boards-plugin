using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PingwayBoards.Windows;

namespace PingwayBoards;

/// <summary>
/// Pingway Boards: a read-only, text-only window with the three Pingway Boards (pingway.app).
/// The only network call is GET https://pingway.app/api/boards-feed (see FeedClient).
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/pboards";

    public Configuration Configuration { get; }
    public Loc Loc { get; }
    public FeedClient Feed { get; }
    public Assets Assets { get; }

    private readonly WindowSystem windowSystem = new("PingwayBoards");
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        // the plugin texts follow the game client language (English, French, German, Japanese)
        this.Loc = new Loc(ClientState.ClientLanguage.ToString());
        this.Feed = new FeedClient(typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0.0.0.0");
        this.Assets = new Assets(PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty);

        this.mainWindow = new MainWindow(this);
        this.configWindow = new ConfigWindow(this);
        this.windowSystem.AddWindow(this.mainWindow);
        this.windowSystem.AddWindow(this.configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = this.Loc.T("pb.cmd.help"),
        });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
        CommandManager.RemoveHandler(CommandName);

        this.windowSystem.RemoveAllWindows();
        this.mainWindow.Dispose();
        this.configWindow.Dispose();
        this.Feed.Dispose();
    }

    private void OnCommand(string command, string args) => this.OpenMainUi();

    public void OpenMainUi() => this.mainWindow.IsOpen = true;

    public void OpenConfigUi() => this.configWindow.IsOpen = true;

    /// <summary>Called by the settings window when "Demo: sample posters" changes.</summary>
    public void DemoModeChanged() => this.mainWindow.RequestRefresh();
}
