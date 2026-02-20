using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace Understudy.Windows.Components;

public class Dashboard
{
    private readonly Plugin plugin;
    private readonly Action<ulong> onCharacterSelected;

    public Dashboard(Plugin plugin, Action<ulong> onCharacterSelected)
    {
        this.plugin = plugin;
        this.onCharacterSelected = onCharacterSelected;
    }

    public void Draw()
    {
        ImGui.Spacing();

        var characters = plugin.Configuration.Characters;
        if (!characters.Any())
        {
            ImGui.Spacing();
            ImGui.Spacing();
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var emptyWidth = ImGui.GetContentRegionAvail().X;
            var msg = "No characters tracked yet. Log in to get started.";
            var textSize = ImGui.CalcTextSize(msg);
            dl.AddText(
                pos + new Vector2((emptyWidth - textSize.X) * 0.5f, 20),
                ImGui.GetColorU32(Theme.TextDisabled), msg);
            ImGui.Dummy(new Vector2(0, 60));
            return;
        }

        var charsList = characters.ToList();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var availHeight = ImGui.GetContentRegionAvail().Y;
        var cardWidth = 320f * ImGui.GetIO().FontGlobalScale;
        var scale = ImGui.GetIO().FontGlobalScale;
        var cardHeight = 170f * scale;
        float spacing = 16f;

        int maxCols = Math.Max(1, (int)((availWidth + spacing) / (cardWidth + spacing)));
        int actualCols = Math.Min(charsList.Count, maxCols);
        int rows = (int)Math.Ceiling((double)charsList.Count / maxCols);
        
        float totalWidth = actualCols * cardWidth + (actualCols - 1) * spacing;
        float totalHeight = rows * cardHeight + (rows - 1) * spacing;
        
        float startX = ImGui.GetCursorPosX() + Math.Max(0, (availWidth - totalWidth) / 2);
        float startY = ImGui.GetCursorPosY() + Math.Max(20, (availHeight - totalHeight) / 2);

        ImGui.SetCursorPosY(startY);

        int col = 0;
        foreach (var kvp in charsList)
        {
            if (col == 0)
            {
                ImGui.SetCursorPosX(startX);
            }
            else
            {
                ImGui.SameLine(0, spacing);
            }

            DrawCharacterCard(kvp.Key, kvp.Value, cardWidth);

            col++;
            if (col >= actualCols)
            {
                col = 0;
                ImGui.Dummy(new Vector2(0, spacing));
            }
        }
    }

    private void DrawCharacterCard(ulong id, CharacterData data, float width)
    {
        var scale = ImGui.GetIO().FontGlobalScale;
        var height = 170f * scale;
        var dl = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var cardEnd = startPos + new Vector2(width, height);
        var padding = 14f;

        // ── Interaction hitbox (placed first for event handling) ──
        ImGui.InvisibleButton($"##Card_{id}", new Vector2(width, height));
        bool isHovered = ImGui.IsItemHovered();
        bool isClicked = ImGui.IsItemClicked();

        if (isClicked) onCharacterSelected(id);
        if (isHovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        // ── Card background ──
        var bgColor = isHovered ? Theme.BgCardHover : Theme.BgCard;
        dl.AddRectFilled(startPos, cardEnd, ImGui.GetColorU32(bgColor), Theme.CardRounding);

        // ── Content layout ──
        float y = startPos.Y + padding;
        float x = startPos.X + padding;
        float innerWidth = width - padding * 2;

        // Character name (larger)
        var nameText = data.Name;
        dl.AddText(null, ImGui.GetFontSize() * 1.2f, new Vector2(x, y),
            ImGui.GetColorU32(Theme.AccentPrimary), nameText);

        // World pill (right-aligned in header)
        var worldName = GetWorldName(data.WorldId);
        var worldSize = ImGui.CalcTextSize(worldName);
        var worldPillMin = new Vector2(startPos.X + width - padding - worldSize.X - 12, y + 2);
        var worldPillMax = worldPillMin + worldSize + new Vector2(12, 4);
        dl.AddRectFilled(worldPillMin, worldPillMax, ImGui.GetColorU32(Theme.BgDark), 8f);
        dl.AddRect(worldPillMin, worldPillMax, ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.5f }), 8f);
        dl.AddText(worldPillMin + new Vector2(6, 2), ImGui.GetColorU32(Theme.TextDisabled), worldName);

        y += ImGui.GetFontSize() * 1.2f + 8;

        // Subtle separator
        dl.AddLine(new Vector2(x, y), new Vector2(x + innerWidth, y),
            ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), 1f);
        y += 6;

        // ── Weekly Tomestones ──
        var tomes = data.Tomestones;
        float ratio = tomes.MnemonicsWeeklyCap > 0 ? (float)tomes.MnemonicsWeekly / tomes.MnemonicsWeeklyCap : 0f;
        dl.AddText(new Vector2(x, y), ImGui.GetColorU32(Theme.TextPrimary), "Weekly Tomestones");
        y += ImGui.GetTextLineHeight() + 4;

        // Progress bar (custom drawn with rounded ends)
        float barHeight = 14f;
        float barWidth = innerWidth;
        var barMin = new Vector2(x, y);
        var barMax = new Vector2(x + barWidth, y + barHeight);
        dl.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Theme.BgDark), barHeight * 0.5f);

        float fillWidth = Math.Max(barHeight, barWidth * Math.Clamp(ratio, 0f, 1f));
        var fillColor = Theme.ProgressColor(ratio);
        dl.AddRectFilled(barMin, new Vector2(barMin.X + fillWidth, barMax.Y),
            ImGui.GetColorU32(fillColor), barHeight * 0.5f);

        // Bar text (centered)
        var barText = $"{tomes.MnemonicsWeekly} / {tomes.MnemonicsWeeklyCap}";
        var barTextSize = ImGui.CalcTextSize(barText);
        dl.AddText(
            barMin + new Vector2((barWidth - barTextSize.X) * 0.5f, (barHeight - barTextSize.Y) * 0.5f),
            ImGui.GetColorU32(Theme.TextPrimary), barText);

        y += barHeight + 10;

        // ── Raid Status + Max IL row ──
        dl.AddText(new Vector2(x, y), ImGui.GetColorU32(Theme.TextSecondary), "Raid Status");
        y += ImGui.GetTextLineHeight() + 4;

        // Raid mini-icons (drawn via DrawList to avoid layout issues)
        float iconSize = 28f;
        float iconSpacing = 6f;
        var raidImageIds = plugin.RaidManager.GetAllRaidImageIds();
        DrawMiniRaidIcon(dl, new Vector2(x, y), iconSize, data.RaidProgress.M1, raidImageIds?.GetValueOrDefault("M1") ?? 0);
        DrawMiniRaidIcon(dl, new Vector2(x + (iconSize + iconSpacing) * 1, y), iconSize, data.RaidProgress.M2, raidImageIds?.GetValueOrDefault("M2") ?? 0);
        DrawMiniRaidIcon(dl, new Vector2(x + (iconSize + iconSpacing) * 2, y), iconSize, data.RaidProgress.M3, raidImageIds?.GetValueOrDefault("M3") ?? 0);
        DrawMiniRaidIcon(dl, new Vector2(x + (iconSize + iconSpacing) * 3, y), iconSize, data.RaidProgress.M4, raidImageIds?.GetValueOrDefault("M4") ?? 0);

        // Max IL (right-aligned, larger text)
        float maxIl = data.GearSets.Values.Any() ? data.GearSets.Values.Max(g => g.AverageItemLevel) : 0;
        var ilText = maxIl > 0 ? $"{maxIl:F0}" : "-";
        var ilLabel = "Max IL";
        var ilLabelSize = ImGui.CalcTextSize(ilLabel);
        float ilX = startPos.X + width - padding - 60;
        dl.AddText(new Vector2(ilX, y - ImGui.GetTextLineHeight() - 4), ImGui.GetColorU32(Theme.TextSecondary), ilLabel);

        var ilColor = maxIl >= Theme.ILThresholdMax ? Theme.ILTierMax
                    : maxIl >= Theme.ILThresholdHigh ? Theme.AccentSuccess
                    : Theme.AccentSecondary;
        dl.AddText(null, ImGui.GetFontSize() * 1.3f, new Vector2(ilX, y),
            ImGui.GetColorU32(ilColor), ilText);

        // ── Hover glow border ──
        if (isHovered)
        {
            dl.AddRect(startPos, cardEnd, ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.6f }),
                Theme.CardRounding, ImDrawFlags.None, 2f);
            // Outer glow
            dl.AddRect(startPos - new Vector2(1, 1), cardEnd + new Vector2(1, 1),
                ImGui.GetColorU32(Theme.GlowPrimary), Theme.CardRounding + 1);
        }
        else
        {
            dl.AddRect(startPos, cardEnd, ImGui.GetColorU32(Theme.BorderCard), Theme.CardRounding);
        }

        // ── Adventurer Plate Frame overlay ──
        DrawFrameOverlay(dl, startPos, width, height);
    }

    private void DrawFrameOverlay(ImDrawListPtr dl, Vector2 pos, float width, float height)
    {
        uint frameId = plugin.Configuration.DashboardFrameImageId;
        if (frameId == 0) return;

        var texPath = $"ui/icon/198000/{frameId:D6}_hr1.tex";
        var tex = Plugin.TextureProvider.GetFromGame(texPath);
        if (!tex.TryGetWrap(out var wrap, out _)) return;

        // Frame textures have built-in transparent margins. Expand the draw area
        // so the decorative border elements align with the card edges.
        var padX = 40f;
        var padY = 24f;
        var padBottom = 8f;
        var frameMin = pos - new Vector2(padX, padY);
        var frameMax = pos + new Vector2(width + padX, height + padBottom);
        var frameTint = ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, plugin.Configuration.DashboardFrameOpacity));
        dl.AddImage(wrap.Handle, frameMin, frameMax, Vector2.Zero, Vector2.One, frameTint);
    }

    private void DrawMiniRaidIcon(ImDrawListPtr dl, Vector2 pos, float size, bool cleared, uint imageId)
    {
        if (imageId != 0)
        {
            var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(imageId));
            if (tex.TryGetWrap(out var wrap, out _))
            {
                var tint = cleared
                    ? ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f))
                    : ImGui.GetColorU32(new Vector4(0.4f, 0.4f, 0.4f, 0.4f));
                dl.AddImage(wrap.Handle, pos, pos + new Vector2(size, size), Vector2.Zero, Vector2.One, tint);

                if (cleared)
                {
                    dl.AddRect(pos, pos + new Vector2(size, size),
                        ImGui.GetColorU32(Theme.AccentSuccess), 3f, ImDrawFlags.None, 1.5f);
                    dl.AddRect(pos - new Vector2(1, 1), pos + new Vector2(size + 1, size + 1),
                        ImGui.GetColorU32(Theme.GlowSuccess), 4f);
                }
                return;
            }
        }

        // Fallback
        var col = cleared ? Theme.AccentSuccess with { W = 0.6f } : new Vector4(0.15f, 0.15f, 0.2f, 0.8f);
        dl.AddRectFilled(pos, pos + new Vector2(size, size), ImGui.GetColorU32(col), 3f);
    }

    private string GetWorldName(uint worldId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        if (sheet != null && sheet.TryGetRow(worldId, out var world))
        {
            return world.Name.ToString();
        }
        return $"#{worldId}";
    }
}
