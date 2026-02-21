using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// Shared state and drawing helpers used by all LoadoutPopup tabs.
/// </summary>
public class LoadoutPopupShared
{
    public ulong? CurrentCharacterId { get; set; }
    public uint ManualJobId { get; set; }
    public int LoadingFrame { get; set; }

    // ── Styled horizontal separator with accent color ──
    public void DrawAccentSeparator()
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        dl.AddLine(pos, pos + new Vector2(width, 0), ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.25f }), 1.0f);
        ImGui.Dummy(new Vector2(0, 1));
    }

    // ── Section header with subtle background band ──
    public void DrawSectionHeader(string title, string? subtitle = null)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = subtitle != null ? 40f : 26f;

        dl.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.GetColorU32(Theme.SectionHeaderBg), 4f);
        dl.AddRectFilled(pos, pos + new Vector2(3, height), ImGui.GetColorU32(Theme.AccentPrimary), 2f);

        dl.AddText(pos + new Vector2(12, 4), ImGui.GetColorU32(Theme.AccentPrimary), title);
        if (subtitle != null)
        {
            dl.AddText(pos + new Vector2(12, 22), ImGui.GetColorU32(Theme.TextSecondary), subtitle);
        }

        ImGui.Dummy(new Vector2(0, height + 6));
    }

    // ── Pill badge via DrawList ──
    public void DrawPill(ImDrawListPtr dl, Vector2 pos, string text, Vector4 color)
    {
        var textSize = ImGui.CalcTextSize(text);
        var padding = new Vector2(8, 3);
        var rectMin = pos;
        var rectMax = pos + textSize + padding * 2;
        dl.AddRectFilled(rectMin, rectMax, ImGui.GetColorU32(color with { W = 0.18f }), 10f);
        dl.AddRect(rectMin, rectMax, ImGui.GetColorU32(color with { W = 0.40f }), 10f);
        dl.AddText(rectMin + padding, ImGui.GetColorU32(color), text);
    }

    // ── Loading spinner indicator ──
    public void DrawLoadingIndicator(string message)
    {
        ImGui.Spacing();
        ImGui.Spacing();

        var dl = ImGui.GetWindowDrawList();
        var center = ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X * 0.5f, 30);

        var dots = new string('.', (LoadingFrame / 20) % 4);
        var fullText = $"{message}{dots}";
        var textSize = ImGui.CalcTextSize(fullText);
        dl.AddText(center - new Vector2(textSize.X * 0.5f, 0), ImGui.GetColorU32(Theme.AccentPrimary), fullText);

        float radius = 8f;
        var arcCenter = center + new Vector2(0, 25);
        float startAngle = (LoadingFrame * 0.05f) % (MathF.PI * 2);
        dl.AddCircle(arcCenter, radius, ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.15f }), 20, 2f);

        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = startAngle + (MathF.PI * 2 * i / segments);
            float nextAngle = startAngle + (MathF.PI * 2 * (i + 1) / segments);
            if (i < segments / 3)
            {
                var p1 = arcCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                var p2 = arcCenter + new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * radius;
                dl.AddLine(p1, p2, ImGui.GetColorU32(Theme.AccentPrimary), 2.5f);
            }
        }

        ImGui.Dummy(new Vector2(0, 65));
    }

    // ── Empty state placeholder ──
    public void DrawEmptyState(string message)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 60f;

        dl.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.GetColorU32(Theme.BgDark), Theme.RowRounding);
        dl.AddRect(pos, pos + new Vector2(width, height), ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), Theme.RowRounding, ImDrawFlags.None, 1f);

        var textSize = ImGui.CalcTextSize(message);
        dl.AddText(
            pos + new Vector2((width - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f),
            ImGui.GetColorU32(Theme.TextDisabled),
            message);

        ImGui.Dummy(new Vector2(0, height + 4));
    }
}
