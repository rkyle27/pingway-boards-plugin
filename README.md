# Pingway Boards

A small Dalamud plugin for FINAL FANTASY XIV that shows the **Pingway Boards** from [pingway.app](https://pingway.app) in an in-game window:

- **Static recruitment**: Looking for members / Looking for a static
- **FC recruitment**: Looking for members / Looking for an FC
- **Community events**: the Featured top 3 first, then the events starting soonest

It has the same filters as the website (search, content, role / job, region / Data Center / world, FC style, event day and sort), and the boards look like the website (cork board, FC board, chalkboard, paper posters).

The plugin is **read-only and text only**. There are no sign-up, recruit, invite or star buttons: each poster has **View full poster**, which opens that poster on pingway.app in your browser after a warning (OK / Cancel). Links on posters are shown as text and also open only after a warning that names the site.

Open it with `/pboards`, or from the Dalamud plugin list. The texts follow your game client language (English, Français, Deutsch, 日本語).

In the settings you can tick **Demo: sample posters** to see built-in, made-up posters without any internet use.

## Install

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this address, tick the box next to it and press **Save**:

   ```
   https://raw.githubusercontent.com/rkyle27/pingway-boards-plugin/main/repo.json
   ```

3. Open `/xlplugins`, search for **Pingway Boards** and install it.

## Privacy

- The plugin only reads the public Boards feed: `GET https://pingway.app/api/boards-feed` (when the window opens, then at most every 2 minutes while it is open).
- No login, no cookies, no telemetry. Nothing about you or your character is sent.
- It does not read other players' data and does not automate anything in the game.
- In demo mode it makes no network requests at all.

## Good to know

- Pingway is not affiliated with or endorsed by Square Enix or Discord. FINAL FANTASY XIV is a registered trademark of Square Enix.
- Third-party tools, including Dalamud plugins, are against the FINAL FANTASY XIV terms of service. This plugin only displays public text, which keeps the risk low, but it is never zero. Use it at your own risk.

## Building

The plugin is built by GitHub Actions (`.github/workflows/build.yml`): .NET 10, the Dalamud dev files from goatcorp's official distribution, and `Dalamud.NET.Sdk` 15.0.0 (the only package). Tagging `v*` publishes a release with `latest.zip` (`.github/workflows/release.yml`).

`python3 tools/check.py` checks the JSON files, the 4 language files, the demo posters and the images without .NET.

The board and poster pictures in `PingwayBoards/Assets` were made from the website's own Boards CSS with `tools/render_assets.py` and `tools/optimize_assets.py`.

## License

[AGPL-3.0](LICENSE)
