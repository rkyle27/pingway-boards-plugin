using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace PingwayBoards;

/// <summary>Colours of one board (taken from the website's Boards CSS).</summary>
public sealed class Theme
{
    public uint Text;        // normal text on the board
    public uint Label;       // filter row labels
    public uint Muted;       // count line, hints
    public uint PanelBg;     // filter panel fill
    public uint PanelBorder; // filter panel border
    public uint ChipBg, ChipText, ChipBorder, ChipOnBg, ChipOnText;
    public uint FrameBg;     // search box
    public uint PopupBg;     // combo pop-up
}

/// <summary>Small drawing helpers for the window (no animations anywhere).</summary>
public static class Ui
{
    /// <summary>A colour from 0-255 RGB and 0-1 alpha, packed for ImGui (ABGR).</summary>
    public static uint Rgba(int r, int g, int b, float a = 1f)
    {
        var alpha = (uint)Math.Clamp((int)Math.Round(a * 255f), 0, 255);
        return (alpha << 24) | ((uint)(b & 255) << 16) | ((uint)(g & 255) << 8) | (uint)(r & 255);
    }

    public static float S => ImGuiHelpers.GlobalScale;

    /// <summary>
    /// Draws an image into a rectangle as a 9-slice: the borders (in image pixels) keep their size times <paramref name="k"/>,
    /// the middle stretches. Rolls, tape, pins and frame edges are therefore never stretched.
    /// </summary>
    public static void DrawSliced(ImDrawListPtr dl, IDalamudTextureWrap tex, Vector2 min, Vector2 max, float left, float top, float right, float bottom, float k)
    {
        float iw = tex.Width, ih = tex.Height;
        if (iw <= 0 || ih <= 0)
            return;
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        // never let the fixed borders overlap
        var kx = Math.Min(k, w / Math.Max(1f, left + right));
        var ky = Math.Min(k, h / Math.Max(1f, top + bottom));
        float[] xs = { min.X, min.X + left * kx, max.X - right * kx, max.X };
        float[] ys = { min.Y, min.Y + top * ky, max.Y - bottom * ky, max.Y };
        float[] us = { 0f, left / iw, (iw - right) / iw, 1f };
        float[] vs = { 0f, top / ih, (ih - bottom) / ih, 1f };
        for (var yi = 0; yi < 3; yi++)
        {
            for (var xi = 0; xi < 3; xi++)
            {
                if (xs[xi + 1] - xs[xi] <= 0.01f || ys[yi + 1] - ys[yi] <= 0.01f)
                    continue;
                dl.AddImage(tex.Handle, new Vector2(xs[xi], ys[yi]), new Vector2(xs[xi + 1], ys[yi + 1]), new Vector2(us[xi], vs[yi]), new Vector2(us[xi + 1], vs[yi + 1]));
            }
        }
    }

    /// <summary>Text centred in the current content width (wraps normally when it is too wide).</summary>
    public static void CenteredText(string text, uint color)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var w = ImGui.CalcTextSize(text).X;
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        if (w <= avail)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - w) / 2f));
            ImGui.TextUnformatted(text);
        }
        else
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + avail);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }

        ImGui.PopStyleColor();
    }

    /// <summary>A row of chips that wraps onto new lines, with a label in front (like the website's filter rows).</summary>
    public sealed class ChipRow
    {
        private readonly Theme theme;
        private readonly float startX;
        private readonly float right;
        private bool first = true;

        /// <param name="theme">The board colours.</param>
        /// <param name="label">The row label ("" = an indented sub-row without a label).</param>
        /// <param name="labelWidth">Width of the label column.</param>
        /// <param name="width">The width the row may use (chips wrap inside it).</param>
        public ChipRow(Theme theme, string label, float labelWidth, float width)
        {
            this.theme = theme;
            var x0 = ImGui.GetCursorPosX();
            this.right = x0 + width;
            ImGui.AlignTextToFramePadding();
            ImGui.PushStyleColor(ImGuiCol.Text, theme.Label);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();
            // SetCursorPosX (not SameLine(offset)): SameLine offsets are relative to the group, cursor positions are not
            ImGui.SameLine();
            ImGui.SetCursorPosX(x0 + labelWidth);
            this.startX = x0 + labelWidth;
        }

        /// <summary>One chip. Returns true when clicked.</summary>
        public bool Chip(string text, bool on, string id, uint? textColor = null)
        {
            var style = ImGui.GetStyle();
            var w = ImGui.CalcTextSize(text).X + (style.FramePadding.X * 2f);
            if (!this.first)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorPosX() + w > this.right)
                {
                    ImGui.NewLine();
                    ImGui.SetCursorPosX(this.startX);
                }
            }

            this.first = false;
            var bg = on ? this.theme.ChipOnBg : this.theme.ChipBg;
            ImGui.PushStyleColor(ImGuiCol.Button, bg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, on ? bg : Mix(bg, this.theme.ChipOnBg, 0.25f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, this.theme.ChipOnBg);
            ImGui.PushStyleColor(ImGuiCol.Text, on ? this.theme.ChipOnText : (textColor ?? this.theme.ChipText));
            ImGui.PushStyleColor(ImGuiCol.Border, on ? this.theme.ChipOnBg : this.theme.ChipBorder);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f * S);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            var clicked = ImGui.Button(text + "##" + id);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(5);
            return clicked;
        }
    }

    /// <summary>Mixes two ImGui colours (t = 0 → a, t = 1 → b).</summary>
    public static uint Mix(uint a, uint b, float t)
    {
        uint Part(uint c, int shift) => (c >> shift) & 255;
        uint Lerp(int shift) => (uint)Math.Round(Part(a, shift) + ((Part(b, shift) - (float)Part(a, shift)) * t)) & 255;
        return (Lerp(24) << 24) | (Lerp(16) << 16) | (Lerp(8) << 8) | Lerp(0);
    }

    /// <summary>A small filled label (role / job / tag), drawn inline like a chip but not clickable.</summary>
    public static void Tag(string text, uint bg, uint fg, uint border, float right, ref bool first)
    {
        var pad = new Vector2(6f * S, 2f * S);
        var size = ImGui.CalcTextSize(text) + (pad * 2f);
        if (!first)
        {
            ImGui.SameLine(0f, 4f * S);
            if (ImGui.GetCursorPosX() + size.X > right)
                ImGui.NewLine();
        }

        first = false;
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, bg, 4f * S);
        if ((border >> 24) != 0)
            dl.AddRect(pos, pos + size, border, 4f * S);
        dl.AddText(pos + pad, fg, text);
        ImGui.Dummy(size);
    }
}
