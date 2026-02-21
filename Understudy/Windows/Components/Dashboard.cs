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
    private readonly System.Action<ulong> onCharacterSelected;

    private ulong? dragSourceId;
    private int dropInsertIndex = -1; // index in CharacterOrder where dragged card would be inserted

    private ulong? pendingDeleteId;
    private string? pendingDeleteName;

    public event System.Action? OnSelectedCharacterDeleted;

    public Dashboard(Plugin plugin, System.Action<ulong> onCharacterSelected)
    {
        this.plugin = plugin;
        this.onCharacterSelected = onCharacterSelected;
    }

    public void Draw()
    {
        ImGui.Spacing();

        DrawReorderToggle();

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

        var order = plugin.Configuration.CharacterOrder;
        var orderedChars = new List<KeyValuePair<ulong, CharacterData>>();
        foreach (var id in order)
        {
            if (characters.TryGetValue(id, out var data))
                orderedChars.Add(new KeyValuePair<ulong, CharacterData>(id, data));
        }
        foreach (var kvp in characters)
        {
            if (!order.Contains(kvp.Key))
                orderedChars.Add(kvp);
        }

        // When dragging, exclude the source card from layout so remaining cards reflow
        var visibleChars = dragSourceId.HasValue
            ? orderedChars.Where(kvp => kvp.Key != dragSourceId.Value).ToList()
            : orderedChars;

        var availWidth = ImGui.GetContentRegionAvail().X;
        var availHeight = ImGui.GetContentRegionAvail().Y;
        var cardWidth = 320f * ImGui.GetIO().FontGlobalScale;
        var scale = ImGui.GetIO().FontGlobalScale;
        var cardHeight = 170f * scale;
        float spacing = 16f;

        int maxCols = Math.Max(1, (int)((availWidth + spacing) / (cardWidth + spacing)));
        int visibleCount = Math.Max(1, visibleChars.Count);
        int actualCols = Math.Min(visibleCount, maxCols);
        int rows = (int)Math.Ceiling((double)visibleCount / maxCols);

        float totalWidth = actualCols * cardWidth + (actualCols - 1) * spacing;
        float totalHeight = rows * cardHeight + (rows - 1) * spacing;

        float startX = ImGui.GetCursorPosX() + Math.Max(0, (availWidth - totalWidth) / 2);
        float startY = ImGui.GetCursorPosY() + Math.Max(20, (availHeight - totalHeight) / 2);

        var windowPos = ImGui.GetWindowPos();
        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();
        float gridScreenX = windowPos.X + startX - scrollX;
        float gridScreenY = windowPos.Y + startY - scrollY;

        ImGui.SetCursorPosY(startY);

        var cardPositions = new List<Vector2>();

        int col = 0;
        for (int i = 0; i < visibleChars.Count; i++)
        {
            var kvp = visibleChars[i];

            if (col == 0)
            {
                ImGui.SetCursorPosX(startX);
            }
            else
            {
                ImGui.SameLine(0, spacing);
            }

            cardPositions.Add(ImGui.GetCursorScreenPos());
            DrawCharacterCard(kvp.Key, kvp.Value, cardWidth, i);

            col++;
            if (col >= actualCols)
            {
                col = 0;
                ImGui.Dummy(new Vector2(0, spacing));
            }
        }

        // ── Grid-based drop zone calculation ──
        dropInsertIndex = -1;
        if (dragSourceId.HasValue && plugin.Configuration.ReorderUnlocked)
        {
            var mousePos = ImGui.GetMousePos();
            var dl = ImGui.GetWindowDrawList();

            // Determine which grid slot the mouse is over (including the empty slot at the end)
            int totalSlots = visibleChars.Count + 1; // +1 for the "after last" slot
            int bestSlot = -1;
            float bestDist = float.MaxValue;

            for (int slot = 0; slot < totalSlots; slot++)
            {
                Vector2 slotCenter;
                if (slot < visibleChars.Count)
                {
                    // Center of existing card
                    slotCenter = cardPositions[slot] + new Vector2(cardWidth * 0.5f, cardHeight * 0.5f);
                }
                else
                {
                    // "After last" slot position
                    int lastCol = visibleChars.Count % maxCols;
                    int lastRow = visibleChars.Count / maxCols;
                    if (lastCol == 0 && visibleChars.Count > 0)
                    {
                        // Last card filled the row, next slot is first column of next row
                        lastCol = 0;
                        lastRow = visibleChars.Count / maxCols;
                    }
                    float slotX = gridScreenX + lastCol * (cardWidth + spacing);
                    float slotY = gridScreenY + lastRow * (cardHeight + spacing);
                    slotCenter = new Vector2(slotX + cardWidth * 0.5f, slotY + cardHeight * 0.5f);
                }

                float dist = Vector2.Distance(mousePos, slotCenter);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSlot = slot;
                }
            }

            // Refine: use left/right half of each card to determine before vs after
            if (bestSlot >= 0 && bestSlot < visibleChars.Count)
            {
                var slotLeft = cardPositions[bestSlot].X;
                if (mousePos.X > slotLeft + cardWidth * 0.5f)
                    bestSlot++; // Insert after this card
            }

            bestSlot = Math.Clamp(bestSlot, 0, visibleChars.Count);
            dropInsertIndex = bestSlot;

            // Draw insertion indicator line
            Vector2 lineTop, lineBottom;
            if (bestSlot < visibleChars.Count)
            {
                // Line to the left of the card at bestSlot
                var pos = cardPositions[bestSlot];
                lineTop = new Vector2(pos.X - spacing * 0.5f, pos.Y);
                lineBottom = new Vector2(pos.X - spacing * 0.5f, pos.Y + cardHeight);
            }
            else if (visibleChars.Count > 0)
            {
                // Line to the right of the last card
                var lastPos = cardPositions[visibleChars.Count - 1];
                lineTop = new Vector2(lastPos.X + cardWidth + spacing * 0.5f, lastPos.Y);
                lineBottom = new Vector2(lastPos.X + cardWidth + spacing * 0.5f, lastPos.Y + cardHeight);
            }
            else
            {
                // No visible cards — indicator at grid start
                lineTop = new Vector2(gridScreenX, gridScreenY);
                lineBottom = new Vector2(gridScreenX, gridScreenY + cardHeight);
            }

            // Vertical insertion line
            dl.AddLine(lineTop, lineBottom,
                ImGui.GetColorU32(Theme.AccentPrimary), 3f);
            // Circles at endpoints
            dl.AddCircleFilled(lineTop, 5f, ImGui.GetColorU32(Theme.AccentPrimary));
            dl.AddCircleFilled(lineBottom, 5f, ImGui.GetColorU32(Theme.AccentPrimary));
            // Glow
            dl.AddLine(lineTop - new Vector2(1, 0), lineBottom - new Vector2(1, 0),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.3f }), 7f);

            // Handle drop on mouse release
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var srcIdx = order.IndexOf(dragSourceId.Value);
                if (srcIdx >= 0)
                {
                    // Map visibleChars insert index back to CharacterOrder index
                    int targetOrderIdx;
                    if (bestSlot < visibleChars.Count)
                    {
                        targetOrderIdx = order.IndexOf(visibleChars[bestSlot].Key);
                    }
                    else if (visibleChars.Count > 0)
                    {
                        // After the last visible card
                        targetOrderIdx = order.IndexOf(visibleChars[visibleChars.Count - 1].Key) + 1;
                    }
                    else
                    {
                        targetOrderIdx = 0;
                    }

                    // Adjust for removal
                    order.RemoveAt(srcIdx);
                    if (targetOrderIdx > srcIdx)
                        targetOrderIdx--;
                    targetOrderIdx = Math.Clamp(targetOrderIdx, 0, order.Count);
                    order.Insert(targetOrderIdx, dragSourceId.Value);
                    plugin.Configuration.Save();
                }
                dragSourceId = null;
            }
        }

        // Clear drag state if mouse released outside unlocked context
        if (dragSourceId.HasValue && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            dragSourceId = null;

        // Delete confirmation modal
        DrawDeleteConfirmation();
    }

    private void DrawCharacterCard(ulong id, CharacterData data, float width, int orderIndex)
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

        // Defer click to mouse release so it doesn't cancel drag-and-drop
        if (isHovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !dragSourceId.HasValue)
            onCharacterSelected(id);
        if (isHovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        // Drag-and-drop source (only when unlocked; drop is handled at grid level)
        if (plugin.Configuration.ReorderUnlocked)
        {
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
            {
                dragSourceId = id;
                ImGui.SetDragDropPayload("CHAR_REORDER", ReadOnlySpan<byte>.Empty);
                ImGui.Text($"Moving {data.Name}...");
                ImGui.EndDragDropSource();
            }
        }

        if (ImGui.BeginPopupContextItem($"DashCardCtx##{id}"))
        {
            if (ImGui.Selectable($"Delete {data.Name}"))
            {
                pendingDeleteId = id;
                pendingDeleteName = data.Name;
            }
            ImGui.EndPopup();
        }

        var bgColor = isHovered ? Theme.BgCardHover : Theme.BgCard;
        dl.AddRectFilled(startPos, cardEnd, ImGui.GetColorU32(bgColor), Theme.CardRounding);

        float y = startPos.Y + padding;
        float x = startPos.X + padding;
        float innerWidth = width - padding * 2;

        var nameText = data.Name;
        dl.AddText(null, ImGui.GetFontSize() * 1.2f, new Vector2(x, y),
            ImGui.GetColorU32(Theme.AccentPrimary), nameText);

        var worldName = GetWorldName(data.WorldId);
        var worldSize = ImGui.CalcTextSize(worldName);
        var worldPillMin = new Vector2(startPos.X + width - padding - worldSize.X - 12, y + 2);
        var worldPillMax = worldPillMin + worldSize + new Vector2(12, 4);
        dl.AddRectFilled(worldPillMin, worldPillMax, ImGui.GetColorU32(Theme.BgDark), 8f);
        dl.AddRect(worldPillMin, worldPillMax, ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.5f }), 8f);
        dl.AddText(worldPillMin + new Vector2(6, 2), ImGui.GetColorU32(Theme.TextDisabled), worldName);

        y += ImGui.GetFontSize() * 1.2f + 8;

        dl.AddLine(new Vector2(x, y), new Vector2(x + innerWidth, y),
            ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), 1f);
        y += 6;

        // ── Weekly Tomestones ──
        var tomes = data.Tomestones;
        float ratio = tomes.MnemonicsWeeklyCap > 0 ? (float)tomes.MnemonicsWeekly / tomes.MnemonicsWeeklyCap : 0f;
        dl.AddText(new Vector2(x, y), ImGui.GetColorU32(Theme.TextPrimary), "Weekly Tomestones");
        y += ImGui.GetTextLineHeight() + 4;

        float barHeight = 14f;
        float barWidth = innerWidth;
        var barMin = new Vector2(x, y);
        var barMax = new Vector2(x + barWidth, y + barHeight);
        dl.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Theme.BgDark), barHeight * 0.5f);

        float fillWidth = Math.Max(barHeight, barWidth * Math.Clamp(ratio, 0f, 1f));
        var fillColor = Theme.ProgressColor(ratio);
        dl.AddRectFilled(barMin, new Vector2(barMin.X + fillWidth, barMax.Y),
            ImGui.GetColorU32(fillColor), barHeight * 0.5f);

        var barText = $"{tomes.MnemonicsWeekly} / {tomes.MnemonicsWeeklyCap}";
        var barTextSize = ImGui.CalcTextSize(barText);
        dl.AddText(
            barMin + new Vector2((barWidth - barTextSize.X) * 0.5f, (barHeight - barTextSize.Y) * 0.5f),
            ImGui.GetColorU32(Theme.TextPrimary), barText);

        y += barHeight + 10;

        // ── Raid Status + Max IL row ──
        dl.AddText(new Vector2(x, y), ImGui.GetColorU32(Theme.TextSecondary), "Raid Status");
        y += ImGui.GetTextLineHeight() + 4;

        float iconSize = 28f;
        float iconSpacing = 6f;
        var raidImageIds = plugin.RaidManager.GetAllRaidImageIds();
        DrawMiniRaidIcon(dl, new Vector2(x, y), iconSize, data.RaidProgress.M1, raidImageIds?.GetValueOrDefault("M1") ?? 0);
        DrawMiniRaidIcon(dl, new Vector2(x + (iconSize + iconSpacing) * 1, y), iconSize, data.RaidProgress.M2, raidImageIds?.GetValueOrDefault("M2") ?? 0);
        DrawMiniRaidIcon(dl, new Vector2(x + (iconSize + iconSpacing) * 2, y), iconSize, data.RaidProgress.M3, raidImageIds?.GetValueOrDefault("M3") ?? 0);
        DrawMiniRaidIcon(dl, new Vector2(x + (iconSize + iconSpacing) * 3, y), iconSize, data.RaidProgress.M4, raidImageIds?.GetValueOrDefault("M4") ?? 0);

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

        // ── Border ──
        if (isHovered && !dragSourceId.HasValue)
        {
            dl.AddRect(startPos, cardEnd, ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.6f }),
                Theme.CardRounding, ImDrawFlags.None, 2f);
            dl.AddRect(startPos - new Vector2(1, 1), cardEnd + new Vector2(1, 1),
                ImGui.GetColorU32(Theme.GlowPrimary), Theme.CardRounding + 1);
        }
        else
        {
            dl.AddRect(startPos, cardEnd, ImGui.GetColorU32(Theme.BorderCard), Theme.CardRounding);
        }

        // ── Adventurer Plate Frame overlay (per-character or global fallback) ──
        DrawFrameOverlay(dl, startPos, width, height, data);
    }

    private void DrawFrameOverlay(ImDrawListPtr dl, Vector2 pos, float width, float height, CharacterData data)
    {
        uint frameId = data.FrameImageId != 0 ? data.FrameImageId : plugin.Configuration.DashboardFrameImageId;
        if (frameId == 0) return;

        float opacity = data.FrameImageId != 0 ? data.FrameOpacity : plugin.Configuration.DashboardFrameOpacity;

        var texPath = $"ui/icon/198000/{frameId:D6}_hr1.tex";
        var tex = Plugin.TextureProvider.GetFromGame(texPath);
        if (!tex.TryGetWrap(out var wrap, out _)) return;

        var padX = 40f;
        var padY = 24f;
        var padBottom = 8f;
        var frameMin = pos - new Vector2(padX, padY);
        var frameMax = pos + new Vector2(width + padX, height + padBottom);
        var frameTint = ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, opacity));
        dl.AddImage(wrap.Handle, frameMin, frameMax, Vector2.Zero, Vector2.One, frameTint);
    }

    private void DrawReorderToggle()
    {
        var unlocked = plugin.Configuration.ReorderUnlocked;
        var icon = unlocked ? FontAwesomeIcon.Unlock : FontAwesomeIcon.Lock;
        var tooltip = unlocked ? "Reordering enabled. Click to lock." : "Reordering locked. Click to unlock.";
        var color = unlocked ? Theme.AccentWarning : Theme.TextDisabled;

        var availWidth = ImGui.GetContentRegionAvail().X;
        float buttonSize = 28f;
        ImGui.SameLine(availWidth - buttonSize);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.BgCard);
        ImGui.PushStyleColor(ImGuiCol.Text, color);

        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button(icon.ToIconString(), new Vector2(buttonSize, buttonSize)))
        {
            plugin.Configuration.ReorderUnlocked = !unlocked;
            plugin.Configuration.Save();
        }
        ImGui.PopFont();
        ImGui.PopStyleColor(4);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void DrawDeleteConfirmation()
    {
        if (pendingDeleteId.HasValue)
        {
            ImGui.OpenPopup("ConfirmDeleteCharacterDash");
            // Only open once — clear after opening so we don't re-open every frame
        }

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("ConfirmDeleteCharacterDash", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.TextColored(Theme.AccentDanger, "Delete Character");
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text($"Are you sure you want to delete {pendingDeleteName ?? "this character"}?");
            ImGui.TextColored(Theme.TextSecondary, "This will remove all gear, BiS, and tracking data.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Delete", new Vector2(120, 0)))
            {
                if (pendingDeleteId.HasValue)
                {
                    plugin.Configuration.Characters.Remove(pendingDeleteId.Value);
                    plugin.Configuration.CharacterOrder.Remove(pendingDeleteId.Value);
                    plugin.Configuration.Save();
                    OnSelectedCharacterDeleted?.Invoke();
                }
                pendingDeleteId = null;
                pendingDeleteName = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                pendingDeleteId = null;
                pendingDeleteName = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
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
