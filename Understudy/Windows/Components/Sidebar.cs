using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using Understudy.Models;

namespace Understudy.Windows.Components;

public class Sidebar
{
    private readonly Plugin plugin;
    private readonly Action<ulong?> onSelectionChanged;
    private readonly Action onConfigRequested;
    private ulong? selectedContentId;
    private readonly ISharedImmediateTexture? logoTexture;

    private ulong? dragSourceId;
    private int dropTargetIndex = -1;

    private ulong? pendingDeleteId;
    private string? pendingDeleteName;

    public Sidebar(Plugin plugin, Action<ulong?> onSelectionChanged, Action onConfigRequested)
    {
        this.plugin = plugin;
        this.onSelectionChanged = onSelectionChanged;
        this.onConfigRequested = onConfigRequested;

        var assemblyDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
        if (assemblyDir != null)
        {
            var logoPath = Path.Combine(assemblyDir, "Assets", "Understudy1.png");
            if (File.Exists(logoPath))
                logoTexture = Plugin.TextureProvider.GetFromFile(logoPath);
        }
    }

    public void UpdateSelection(ulong? contentId)
    {
        selectedContentId = contentId;
    }

    public void Draw()
    {
        var drawList = ImGui.GetWindowDrawList();

        ImGui.Spacing();
        ImGui.Indent(12);

        if (logoTexture != null && logoTexture.TryGetWrap(out var logoWrap, out _))
        {
            var logoSize = 128f * ImGui.GetIO().FontGlobalScale;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - logoSize) * 0.5f - 12);
            ImGui.Image(logoWrap.Handle, new Vector2(logoSize, logoSize));
        }
        else
        {
            ImGui.Spacing();
        }

        ImGui.Unindent(12);

        ImGui.Dummy(new Vector2(0, 10));
        var lineStart = ImGui.GetCursorScreenPos();
        var lineWidth = ImGui.GetContentRegionAvail().X;
        drawList.AddLine(
            lineStart,
            lineStart + new Vector2(lineWidth, 0),
            ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.4f }),
            1.5f);

        ImGui.Spacing();
        ImGui.Spacing();

        DrawSidebarItem(null, "Dashboard", FontAwesomeIcon.ChartLine, selectedContentId == null);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(Theme.TextDisabled, "  CHARACTERS");
        ImGui.Spacing();

        var order = plugin.Configuration.CharacterOrder;
        var characters = plugin.Configuration.Characters;
        dropTargetIndex = -1;

        var visibleItems = new List<(ulong id, CharacterData data)>();
        var itemPositions = new List<(Vector2 min, Vector2 max)>();

        for (int i = 0; i < order.Count; i++)
        {
            var id = order[i];
            if (!characters.TryGetValue(id, out var data)) continue;

            // Skip the dragged character so remaining items reflow
            if (dragSourceId.HasValue && dragSourceId.Value == id) continue;

            var isSelected = selectedContentId == id;
            DrawSidebarItem(id, data.Name, FontAwesomeIcon.User, isSelected, GetWorldName(data.WorldId));

            itemPositions.Add((ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
            visibleItems.Add((id, data));

            // Drag-and-drop source (only when unlocked; drop is handled below)
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

            if (ImGui.BeginPopupContextItem($"SidebarCtx##{id}"))
            {
                if (ImGui.Selectable($"Delete {data.Name}"))
                {
                    pendingDeleteId = id;
                    pendingDeleteName = data.Name;
                    ImGui.OpenPopup("ConfirmDeleteCharacter");
                }
                ImGui.EndPopup();
            }
        }

        // ── Position-based drop zone calculation ──
        if (dragSourceId.HasValue && plugin.Configuration.ReorderUnlocked && visibleItems.Count > 0)
        {
            var mousePos = ImGui.GetMousePos();

            // Find insertion index based on mouse Y position
            int insertIdx = visibleItems.Count; // default: after last item
            for (int i = 0; i < visibleItems.Count; i++)
            {
                var midY = (itemPositions[i].min.Y + itemPositions[i].max.Y) * 0.5f;
                if (mousePos.Y < midY)
                {
                    insertIdx = i;
                    break;
                }
            }
            dropTargetIndex = insertIdx;

            Vector2 lineLeft, lineRight;
            float lineW;
            if (insertIdx < visibleItems.Count)
            {
                var itemMin = itemPositions[insertIdx].min;
                var itemMax = itemPositions[insertIdx].max;
                lineW = itemMax.X - itemMin.X;
                lineLeft = itemMin - new Vector2(0, 2);
                lineRight = itemMin + new Vector2(lineW, -2);
            }
            else
            {
                var lastMax = itemPositions[visibleItems.Count - 1].max;
                var lastMin = itemPositions[visibleItems.Count - 1].min;
                lineW = lastMax.X - lastMin.X;
                lineLeft = new Vector2(lastMin.X, lastMax.Y + 2);
                lineRight = new Vector2(lastMin.X + lineW, lastMax.Y + 2);
            }

            drawList.AddLine(lineLeft, lineRight, ImGui.GetColorU32(Theme.AccentPrimary), 3f);
            drawList.AddCircleFilled(lineLeft, 4f, ImGui.GetColorU32(Theme.AccentPrimary));
            drawList.AddLine(lineLeft - new Vector2(0, 1), lineRight - new Vector2(0, 1),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.3f }), 7f);

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var srcIdx = order.IndexOf(dragSourceId.Value);
                if (srcIdx >= 0)
                {
                    // Map visible insert index to CharacterOrder index
                    int targetOrderIdx;
                    if (insertIdx < visibleItems.Count)
                    {
                        targetOrderIdx = order.IndexOf(visibleItems[insertIdx].id);
                    }
                    else
                    {
                        targetOrderIdx = order.IndexOf(visibleItems[visibleItems.Count - 1].id) + 1;
                    }

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

        if (dragSourceId.HasValue && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            dragSourceId = null;

        DrawDeleteConfirmation();

        var remaining = ImGui.GetContentRegionAvail().Y;
        if (remaining > 50)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + remaining - 50);

            var sepStart = ImGui.GetCursorScreenPos();
            drawList.AddLine(
                sepStart,
                sepStart + new Vector2(lineWidth, 0),
                ImGui.GetColorU32(Theme.BorderSubtle),
                1.0f);
            ImGui.Spacing();

            DrawSidebarItem(99999, "Settings", FontAwesomeIcon.Cog, false, null, true);
        }
    }

    private void DrawDeleteConfirmation()
    {
        if (pendingDeleteId.HasValue)
        {
            ImGui.OpenPopup("ConfirmDeleteCharacter");
        }

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("ConfirmDeleteCharacter", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
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
                    if (selectedContentId == pendingDeleteId.Value)
                    {
                        selectedContentId = null;
                        onSelectionChanged(null);
                    }
                    plugin.Configuration.Save();
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

    private void DrawSidebarItem(ulong? id, string label, FontAwesomeIcon icon, bool isSelected, string? subtext = null, bool isSettings = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        float height = subtext != null ? 38f : 30f;

        ImGui.InvisibleButton($"##Sidebar_{label}_{id}", new Vector2(width, height));
        bool isHovered = ImGui.IsItemHovered();

        // Defer click to mouse release so it doesn't cancel drag-and-drop
        if (isHovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !dragSourceId.HasValue)
        {
            if (isSettings)
                onConfigRequested();
            else
            {
                selectedContentId = id;
                onSelectionChanged(id);
            }
        }

        if (isSelected)
        {
            drawList.AddRectFilled(startPos, startPos + new Vector2(width, height),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.15f }), 4f);

            drawList.AddRectFilled(startPos + new Vector2(0, 4), startPos + new Vector2(3, height - 4),
                 ImGui.GetColorU32(Theme.AccentPrimary), 2f);
        }
        else if (isHovered)
        {
             drawList.AddRectFilled(startPos, startPos + new Vector2(width, height),
                ImGui.GetColorU32(Theme.BgCardHover), 4f);
        }

        float iconSize = 16f;
        var iconPos = startPos + new Vector2(10, (height - iconSize) * 0.5f);

        ImGui.PushFont(UiBuilder.IconFont);
        string iconStr = icon.ToIconString();
        var iconColor = isSelected ? Theme.AccentPrimary : (isHovered ? Theme.TextPrimary : Theme.TextSecondary);
        drawList.AddText(UiBuilder.IconFont, 16f, iconPos, ImGui.GetColorU32(iconColor), iconStr);
        ImGui.PopFont();

        var textPos = startPos + new Vector2(34, (height - ImGui.GetTextLineHeight()) * 0.5f);
        if (subtext != null)
            textPos = startPos + new Vector2(34, 4);

        var textColor = isSelected ? Theme.TextPrimary : (isHovered ? Theme.TextPrimary : Theme.TextSecondary);
        drawList.AddText(textPos, ImGui.GetColorU32(textColor), label);

        if (subtext != null)
        {
             var subPos = startPos + new Vector2(34, 18);
             drawList.AddText(null, 0.9f * ImGui.GetFontSize(), subPos, ImGui.GetColorU32(Theme.TextDisabled), subtext);
        }
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
