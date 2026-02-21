using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// Renders a single combined loadout card for a job, showing current gear vs BiS
/// targets with collapsible header bar and expanded content.
/// </summary>
public class LoadoutCard
{
    private readonly Plugin plugin;
    private readonly BiSComparisonView bisView;
    private readonly MateriaDisplay materiaDisplay;

    public LoadoutCard(Plugin plugin, BiSComparisonView bisView, MateriaDisplay materiaDisplay)
    {
        this.plugin = plugin;
        this.bisView = bisView;
        this.materiaDisplay = materiaDisplay;
    }

    public void Draw(uint jobId, GearSetData? gearSet, BiSData? bisData, ref uint? jobToRemove, ref uint? bisToRemove, ref uint? dragSourceJob, ref uint? dragTargetJob, ulong? characterId = null)
    {
        var dl = ImGui.GetWindowDrawList();
        var jobAbbr = SharedDrawHelpers.GetJobAbbreviation(jobId);

        float avgIL = gearSet?.AverageItemLevel ?? 0f;
        var ilText = avgIL > 0 ? $"IL {avgIL:F0}" : "No Gear";

        var ilColor = avgIL >= Theme.ILThresholdMax ? Theme.ILTierMax
                    : avgIL >= Theme.ILThresholdHigh ? Theme.AccentSuccess
                    : avgIL >= Theme.ILThresholdMid ? Theme.AccentWarning
                    : Theme.TextSecondary;

        List<BiSSlotComparison>? comparisons = null;
        AcquisitionCosts? costs = null;
        int ownedCount = 0;
        int totalSlots = 0;

        if (bisData != null && bisData.Items.Count > 0)
        {
            var result = plugin.BiSManager.Compare(jobId, characterId);
            comparisons = result.Comparisons;
            costs = result.Costs;
            ownedCount = comparisons.Count(c => c.IsOwned);
            totalSlots = comparisons.Count;
        }

        bool hasBiS = comparisons != null && totalSlots > 0;

        // ── Header Bar ──
        var startPos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X - Theme.CardPadding * 2;
        var headerRect = new Vector2(width, Theme.GearHeaderHeight);

        ImGui.InvisibleButton($"##LoadoutHeader_{jobId}", headerRect);
        bool isHovered = ImGui.IsItemHovered();
        bool clicked = isHovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left);

        if (plugin.Configuration.ReorderUnlocked)
        {
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
            {
                dragSourceJob = jobId;
                ImGui.SetDragDropPayload("LOADOUT_REORDER", ReadOnlySpan<byte>.Empty);
                ImGui.Text($"Moving {jobAbbr}...");
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                dl.AddRectFilled(startPos, startPos + headerRect,
                    ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.12f }), 4f);
                dl.AddRect(startPos, startPos + headerRect,
                    ImGui.GetColorU32(Theme.AccentPrimary), 4f, ImDrawFlags.None, 2.5f);

                dl.AddLine(startPos - new Vector2(0, 2), startPos + new Vector2(headerRect.X, -2),
                    ImGui.GetColorU32(Theme.AccentPrimary), 3f);
                dl.AddCircleFilled(startPos - new Vector2(0, 2), 4f,
                    ImGui.GetColorU32(Theme.AccentPrimary));

                ImGui.AcceptDragDropPayload("LOADOUT_REORDER");
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    dragTargetJob = jobId;
                }
                ImGui.EndDragDropTarget();
            }
        }

        if (ImGui.BeginPopupContextItem($"LoadoutCtx##{jobId}"))
        {
            if (gearSet != null)
            {
                string deleteLabel = hasBiS ? $"Delete [{jobAbbr}] Loadout (Gear + BiS)" : $"Delete [{jobAbbr}] Gearset";
                if (ImGui.Selectable(deleteLabel)) jobToRemove = jobId;
            }
            if (hasBiS)
            {
                if (ImGui.Selectable($"Delete [{jobAbbr}] BiS Only")) bisToRemove = jobId;
            }
            ImGui.EndPopup();
        }

        var storageId = ImGui.GetID($"LoadoutOpen_{jobId}");
        var storage = ImGui.GetStateStorage();
        bool isOpen = storage.GetBool(storageId, true);
        if (clicked)
        {
            isOpen = !isOpen;
            storage.SetBool(storageId, isOpen);
        }

        var barColor = isHovered ? Theme.GearHeaderHover : Theme.GearHeaderBar;
        var barEnd = startPos + headerRect;
        var rounding = Theme.GearCardRounding;
        var flags = isOpen
            ? (ImDrawFlags.RoundCornersTopLeft | ImDrawFlags.RoundCornersTopRight)
            : ImDrawFlags.RoundCornersAll;
        dl.AddRectFilled(startPos, barEnd, ImGui.GetColorU32(barColor), rounding, flags);

        dl.AddLine(
            startPos + new Vector2(0, 4),
            startPos + new Vector2(0, headerRect.Y - 4),
            ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.7f }),
            2.5f);

        float iconSize = 24f;
        float iconPadY = (headerRect.Y - iconSize) * 0.5f;
        var iconPos = startPos + new Vector2(12, iconPadY);
        bool drewJobIcon = false;

        var classJobSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
        if (classJobSheet != null && classJobSheet.TryGetRow(jobId, out var cjRow))
        {
            uint jobIconId = 62100u + jobId;
            var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(jobIconId));
            if (tex.TryGetWrap(out var wrap, out _))
            {
                dl.AddImage(wrap.Handle, iconPos, iconPos + new Vector2(iconSize, iconSize));
                drewJobIcon = true;
            }
        }

        if (!drewJobIcon)
        {
            dl.AddRectFilled(iconPos, iconPos + new Vector2(iconSize, iconSize),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.3f }), 4f);
        }

        float textStartX = iconPos.X + iconSize + 8;
        float textPadY = (headerRect.Y - ImGui.GetTextLineHeight()) * 0.5f;

        var abbrSize = ImGui.CalcTextSize(jobAbbr);
        var badgePos = new Vector2(textStartX, startPos.Y + textPadY - 2);
        var badgeEnd = badgePos + abbrSize + new Vector2(12, 4);
        dl.AddRectFilled(badgePos, badgeEnd, ImGui.GetColorU32(Theme.JobBadgeBg), 4f);
        dl.AddText(badgePos + new Vector2(6, 2), ImGui.GetColorU32(Theme.AccentPrimary), jobAbbr);

        Vector2 currentPillEnd = badgeEnd;

        if (avgIL > 0)
        {
            var ilSize = ImGui.CalcTextSize(ilText);
            var pillPos = new Vector2(currentPillEnd.X + 10, startPos.Y + textPadY - 2);
            var pillEnd = pillPos + ilSize + new Vector2(14, 4);
            dl.AddRectFilled(pillPos, pillEnd, ImGui.GetColorU32(ilColor with { W = 0.15f }), 10f);
            dl.AddRect(pillPos, pillEnd, ImGui.GetColorU32(ilColor with { W = 0.3f }), 10f);
            dl.AddText(pillPos + new Vector2(7, 2), ImGui.GetColorU32(ilColor), ilText);
            currentPillEnd = pillEnd;
        }

        if (hasBiS)
        {
            var bisText = $"{ownedCount}/{totalSlots} BiS";
            var bisColor = ownedCount == totalSlots
                ? Theme.AccentSuccess
                : ownedCount >= totalSlots * 0.5f
                    ? Theme.AccentWarning
                    : Theme.AccentDanger;

            var bisSize = ImGui.CalcTextSize(bisText);
            var bisPillPos = new Vector2(currentPillEnd.X + 8, startPos.Y + textPadY - 2);
            var bisPillEnd = bisPillPos + bisSize + new Vector2(14, 4);
            dl.AddRectFilled(bisPillPos, bisPillEnd, ImGui.GetColorU32(bisColor with { W = 0.15f }), 10f);
            dl.AddRect(bisPillPos, bisPillEnd, ImGui.GetColorU32(bisColor with { W = 0.4f }), 10f);
            dl.AddText(bisPillPos + new Vector2(7, 2), ImGui.GetColorU32(bisColor), bisText);
            currentPillEnd = bisPillEnd;
        }

        var arrowIcon = isOpen ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight;
        var arrowSize = 16f; // FontAwesome icon size
        var arrowPos = new Vector2(barEnd.X - arrowSize - 10, startPos.Y + (headerRect.Y - arrowSize) * 0.5f);

        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(UiBuilder.IconFont, arrowSize, arrowPos, ImGui.GetColorU32(Theme.TextDisabled), arrowIcon.ToIconString());
        ImGui.PopFont();

        DateTime lastUpdated = gearSet?.LastUpdated ?? bisData?.LastUpdated ?? default;
        if (lastUpdated != default)
        {
            var timeText = $"Last Updated: {lastUpdated:MMM dd HH:mm}";
            var timeSize = ImGui.CalcTextSize(timeText);
            var timePos = new Vector2(arrowPos.X - timeSize.X - 12, startPos.Y + textPadY);
            dl.AddText(timePos, ImGui.GetColorU32(Theme.TextDisabled), timeText);
        }

        if (isHovered)
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(Theme.AccentPrimary, $"{jobAbbr} — {ilText}");
            if (hasBiS)
                ImGui.TextColored(Theme.TextSecondary, $"BiS progress: {ownedCount}/{totalSlots} slots");
            ImGui.TextColored(Theme.TextSecondary, "Click to expand/collapse");
            ImGui.TextColored(Theme.TextDisabled, "Right-click to delete");
            ImGui.EndTooltip();
        }

        // ── Expanded Content ──
        if (isOpen)
        {
            var contentStart = ImGui.GetCursorScreenPos();
            ImGui.BeginGroup();
            ImGui.Spacing();

            if (hasBiS)
            {
                bisView.DrawComparatorSplit(comparisons!, bisData);
                ImGui.Spacing();
                if (costs != null)
                    bisView.DrawCompactCostSummary(costs);
                if (bisData != null)
                    bisView.DrawStatSummary(bisData.Items, bisData.FoodId);
            }
            else if (gearSet != null)
            {
                DrawGearSplit(gearSet);
                ImGui.Spacing();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(Theme.LinkAction, FontAwesomeIcon.ArrowRight.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine(0, 4);
                ImGui.TextColored(Theme.LinkAction, "Set up a BiS target to see slot-by-slot comparison");
            }
            else
            {
                ImGui.TextColored(Theme.TextDisabled, "  Track a gearset or import BiS to see details.");
            }

            ImGui.Spacing();
            ImGui.EndGroup();

            var contentEnd = ImGui.GetItemRectMax();
            var cMin = new Vector2(startPos.X, contentStart.Y);
            var cMax = new Vector2(startPos.X + width, contentEnd.Y + 4);

            dl.AddRectFilled(cMin, cMax,
                ImGui.GetColorU32(Theme.GearHeaderBar with { W = 0.4f }),
                0f, ImDrawFlags.RoundCornersBottom);

            dl.AddLine(
                cMax with { X = startPos.X },
                cMax,
                ImGui.GetColorU32(Theme.BorderSubtle),
                1f);
        }

        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawGearSplit(GearSetData gearSet)
    {
        var width = ImGui.GetContentRegionAvail().X - Theme.CardPadding * 2;
        if (ImGui.BeginTable("GearSplit", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg, new Vector2(width, 0)))
        {
            try
            {
                ImGui.TableSetupColumn("Left Side");
                ImGui.TableSetupColumn("Right Side");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                DrawGearColumn(gearSet, new[] { 0, 1, 2, 3, 4, 6, 7 });

                ImGui.TableNextColumn();
                DrawGearColumn(gearSet, new[] { 8, 9, 10, 11, 12 });
            }
            finally
            {
                ImGui.EndTable();
            }
        }
    }

    private void DrawGearColumn(GearSetData gearSet, int[] slots)
    {
        foreach (var slotId in slots)
        {
            var item = gearSet.Items.FirstOrDefault(x => x.Slot == slotId);

            if (slotId == 1 && item == null)
                continue;

            ImGui.AlignTextToFramePadding();

            if (item != null)
            {
                SharedDrawHelpers.DrawItemIcon(item.ItemId);
                ImGui.SameLine();

                var ilColor = item.ItemLevel >= Theme.ILThresholdHigh ? Theme.AccentSuccess
                            : item.ItemLevel >= Theme.ILThresholdMid ? Theme.AccentWarning
                            : Theme.TextPrimary;

                ImGui.TextColored(ilColor, item.Name);

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(Theme.AccentSecondary, $"IL {item.ItemLevel}");
                    ImGui.EndTooltip();
                }
            }
            else
            {
                ImGui.Dummy(new Vector2(24, 24));
                ImGui.SameLine();
                ImGui.TextColored(Theme.TextDisabled, $"{SharedDrawHelpers.GetSlotName(slotId)}  -");
            }
        }
    }
}
