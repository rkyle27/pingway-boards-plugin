using System.IO;
using Dalamud.Interface.Textures.TextureWraps;

namespace PingwayBoards;

/// <summary>
/// The board and poster backgrounds (PNG files in the plugin's Assets folder), loaded with Dalamud's texture provider.
/// The images were made from the website's own Boards CSS (see tools/render_assets.py and tools/optimize_assets.py).
/// </summary>
public sealed class Assets
{
    // Blank poster images: 308 x 442 px with the card itself at (14, 18) .. (294, 418); the rest is the soft shadow / pins / tape
    public const float PosterImageW = 308f, PosterImageH = 442f;
    public const float PosterCardX = 14f, PosterCardY = 18f, PosterCardW = 280f, PosterCardH = 400f;

    // Sign plank of the Static board: 318 x 40 px, nails near both ends
    public const float SignW = 318f, SignH = 40f;

    private readonly string dir;

    public Assets(string pluginDir)
    {
        this.dir = Path.Combine(pluginDir, "Assets");
    }

    /// <summary>The texture, or null while it loads (or if the file is missing): the window then simply draws without it.</summary>
    public IDalamudTextureWrap? Get(string fileName)
    {
        var path = Path.Combine(this.dir, fileName);
        return Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
    }
}
