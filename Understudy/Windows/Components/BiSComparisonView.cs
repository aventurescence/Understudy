using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// Renders BiS progress bar, slot-by-slot comparison, and acquisition cost summary.
/// </summary>
public class BiSComparisonView
{
    private readonly Plugin plugin;
    private readonly MateriaDisplay materiaDisplay;

    public BiSComparisonView(Plugin plugin, MateriaDisplay materiaDisplay)
    {
        this.plugin = plugin;
        this.materiaDisplay = materiaDisplay;
    }



    public void DrawComparatorSplit(List<BiSSlotComparison> comparisons, BiSData? bisData)
    {
        var width = ImGui.GetContentRegionAvail().X - Theme.CardPadding * 2;
        if (ImGui.BeginTable("CmpSplit", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg, new Vector2(width, 0)))
        {
            try
            {
                ImGui.TableSetupColumn("Left Side");
                ImGui.TableSetupColumn("Right Side");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                DrawComparatorColumn(comparisons, new[] { 0, 1, 2, 3, 4, 6, 7 });

                ImGui.TableNextColumn();
                DrawComparatorColumn(comparisons, new[] { 8, 9, 10, 11, 12 });
                
                if (bisData != null && bisData.FoodId != 0)
                {
                    ImGui.Spacing();
                    ImGui.AlignTextToFramePadding();
                    SharedDrawHelpers.DrawItemIcon(bisData.FoodId, 32f);
                    ImGui.SameLine();

                    var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                    if (itemSheet != null && itemSheet.TryGetRow(bisData.FoodId, out var foodItem))
                    {
                        ImGui.TextColored(Theme.AccentPrimary, foodItem.Name.ToString());
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.TextDisabled, "(BiS Food)");

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextColored(Theme.AccentPrimary, foodItem.Name.ToString());
                            ImGui.TextColored(Theme.TextDisabled, "(HQ)");
                            ImGui.Separator();
                            var foodInfo = plugin.StatCalculator.GetFoodStatInfo(bisData.FoodId);
                            foreach (var fi in foodInfo)
                            {
                                if (fi.IsRelative)
                                    ImGui.TextColored(Theme.TextSecondary, $"{fi.StatName}: +{fi.Percent}%% (max {fi.MaxHQ})");
                                else
                                    ImGui.TextColored(Theme.TextSecondary, $"{fi.StatName}: +{fi.MaxHQ}");
                            }
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
            finally
            {
                ImGui.EndTable();
            }
        }
    }

    private void DrawComparatorColumn(List<BiSSlotComparison> comparisons, int[] slots)
    {
        foreach (var slotId in slots)
        {
            var comp = comparisons.FirstOrDefault(c => c.SlotId == slotId);

            if (slotId == 1 && (comp == null || (comp.CurrentItem == null && comp.BiSItem == null)))
                continue;

            ImGui.AlignTextToFramePadding();

            if (comp != null && comp.CurrentItem != null)
            {
                SharedDrawHelpers.DrawItemIcon(comp.CurrentItem.ItemId, 32f);
                ImGui.SameLine();

                if (comp.IsOwned)
                {
                    ImGui.TextColored(Theme.AccentSuccess, comp.CurrentItem.Name);
                    bool ownedHovered = ImGui.IsItemHovered();
                    ImGui.SameLine();
                    materiaDisplay.DrawMateriaRow(comp.CurrentItem.Materia, comp.BiSItem?.Materia);

                    if (ownedHovered)
                        DrawGearStatsTooltip(comp.CurrentItem.ItemId, comp.CurrentItem.Materia);
                }
                else
                {
                    ImGui.TextColored(Theme.AccentDanger, comp.CurrentItem.Name);
                    bool nameHovered = ImGui.IsItemHovered();

                    ImGui.SameLine();
                    materiaDisplay.DrawMateriaOnly(comp.CurrentItem.Materia);

                    if (nameHovered && comp.BiSItem != null)
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextColored(Theme.AccentPrimary, "Best in Slot Target:");
                        ImGui.Text(comp.BiSItem.Name);
                        ImGui.TextColored(Theme.TextSecondary, comp.BiSItem.Source.ToString());
                        if (!string.IsNullOrEmpty(comp.AcquisitionLabel))
                            ImGui.TextColored(Theme.TextDisabled, $"Source: {comp.AcquisitionLabel}");
                        ImGui.Separator();
                        DrawGearStatsTooltipContent(comp.BiSItem.ItemId, comp.BiSItem.Materia);
                        ImGui.EndTooltip();
                    }
                }
            }
            else
            {
                ImGui.Dummy(new Vector2(32, 32));
                ImGui.SameLine();
                ImGui.TextColored(Theme.AccentDanger, $"{SharedDrawHelpers.GetSlotName(slotId)}  -");

                if (ImGui.IsItemHovered() && comp?.BiSItem != null)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(Theme.AccentPrimary, "Best in Slot Target:");
                    ImGui.Text(comp.BiSItem.Name);
                    ImGui.TextColored(Theme.TextSecondary, comp.BiSItem.Source.ToString());
                    if (!string.IsNullOrEmpty(comp.AcquisitionLabel))
                        ImGui.TextColored(Theme.TextDisabled, $"Source: {comp.AcquisitionLabel}");
                    ImGui.Separator();
                    DrawGearStatsTooltipContent(comp.BiSItem.ItemId, comp.BiSItem.Materia);
                    ImGui.EndTooltip();
                }
            }
        }
    }

    public void DrawCompactCostSummary(AcquisitionCosts costs)
    {
        bool hasCosts = costs.TomestonesNeeded > 0
            || costs.UniversalTomestonesNeeded > 0
            || costs.BooksNeeded.Values.Any(v => v > 0)
            || costs.TwineNeeded > 0
            || costs.GlazeNeeded > 0
            || costs.SolventNeeded > 0;

        if (!hasCosts) return;

        ImGui.TextColored(Theme.TextSecondary, "  Remaining:");
        ImGui.SameLine();

        if (costs.TomestonesNeeded > 0)
        {
            DrawCostPill(Theme.AccentDanger, $"{costs.TomestonesNeeded} Mnemonics");
            ImGui.SameLine(0, 6);
        }

        if (costs.UniversalTomestonesNeeded > 0)
        {
            SharedDrawHelpers.DrawIconWithCount(plugin.MiscellanyManager.GetUniversalTomestoneIconId(), 28, costs.UniversalTomestonesNeeded, "Universal Tomestone");
            ImGui.SameLine(0, 6);
        }

        foreach (var k in costs.BooksNeeded.Keys)
        {
            if (costs.BooksNeeded[k] > 0)
            {
                SharedDrawHelpers.DrawIconWithCount(plugin.MiscellanyManager.GetBookIconId(k), 28, costs.BooksNeeded[k], $"{k} Books");
                ImGui.SameLine(0, 6);
            }
        }

        if (costs.TwineNeeded > 0)
        {
            SharedDrawHelpers.DrawIconWithCount(plugin.MiscellanyManager.GetTwineIconId(), 28, costs.TwineNeeded, "Twine");
            ImGui.SameLine(0, 6);
        }

        if (costs.GlazeNeeded > 0)
        {
            SharedDrawHelpers.DrawIconWithCount(plugin.MiscellanyManager.GetGlazeIconId(), 28, costs.GlazeNeeded, "Glaze");
            ImGui.SameLine(0, 6);
        }

        if (costs.SolventNeeded > 0)
        {
            SharedDrawHelpers.DrawIconWithCount(plugin.MiscellanyManager.GetSolventIconId(), 28, costs.SolventNeeded, "Solvent");
            ImGui.SameLine(0, 6);
        }

        ImGui.NewLine();
    }

    // ── Stat Display ──────────────────────────────────────────────

    private void DrawGearStatsTooltip(uint itemId, List<uint>? materia)
    {
        ImGui.BeginTooltip();
        DrawGearStatsTooltipContent(itemId, materia);
        ImGui.EndTooltip();
    }

    private void DrawGearStatsTooltipContent(uint itemId, List<uint>? materia)
    {
        var (stats, materiaDetails) = plugin.StatCalculator.CalcMeldedStats(itemId, materia);
        var baseStats = plugin.StatCalculator.CalcGearStats(itemId);

        ImGui.TextColored(Theme.TextSecondary, "Stats:");
        foreach (var (paramId, totalValue) in stats.OrderBy(kv => kv.Key))
        {
            var name = plugin.StatCalculator.GetStatName(paramId);
            var baseVal = baseStats.TryGetValue(paramId, out var bv) ? bv : 0;
            var meldVal = totalValue - baseVal;

            if (meldVal > 0)
                ImGui.TextColored(Theme.TextPrimary, $"  {name}: {baseVal} + {meldVal} = {totalValue}");
            else
                ImGui.TextColored(Theme.TextPrimary, $"  {name}: {totalValue}");
        }

        if (materiaDetails.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.TextSecondary, "Materia:");
            foreach (var md in materiaDetails)
            {
                var color = md.IsCapped ? Theme.AccentWarning : Theme.AccentSecondary;
                var capText = md.IsCapped ? $" (capped, {md.RawValue - md.EffectiveValue} lost)" : "";
                ImGui.TextColored(color, $"  +{md.EffectiveValue} {md.StatName}{capText}");
            }
        }
    }

    public void DrawStatSummary(Dictionary<int, BiSItem> items, uint foodId)
    {
        var totals = plugin.StatCalculator.CalcFullSetStats(items, foodId);
        if (totals.Count == 0) return;

        var substatOrder = new uint[] { 27, 44, 22, 19, 6, 45, 46 }; // CRT, DET, DH, TEN, PIE, SKS, SPS
        var mainStats = new uint[] { 1, 2, 3, 4, 5 }; // STR, DEX, VIT, INT, MND

        ImGui.Spacing();
        ImGui.TextColored(Theme.TextSecondary, "  Stat Totals:");
        ImGui.SameLine();

        bool first = true;
        foreach (var paramId in substatOrder)
        {
            if (!totals.TryGetValue(paramId, out var value) || value == 0) continue;
            var name = plugin.StatCalculator.GetStatName(paramId);
            var abbr = paramId switch
            {
                27 => "CRT",
                44 => "DET",
                22 => "DH",
                19 => "TEN",
                6 => "PIE",
                45 => "SKS",
                46 => "SPS",
                _ => name
            };
            if (!first) ImGui.SameLine(0, 6);
            DrawCostPill(Theme.AccentSecondary, $"{abbr} {value}");
            first = false;
        }

        if (first) return; // no stats to show
        ImGui.NewLine();
    }

    private void DrawCostPill(Vector4 color, string text)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = ImGui.CalcTextSize(text);
        var pillPad = new Vector2(8, 2);

        dl.AddRectFilled(pos, pos + size + pillPad * 2,
            ImGui.GetColorU32(Theme.CostPillBg), 8f);
        dl.AddRect(pos, pos + size + pillPad * 2,
            ImGui.GetColorU32(color with { W = 0.35f }), 8f);
        dl.AddText(pos + pillPad, ImGui.GetColorU32(color), text);

        ImGui.Dummy(size + pillPad * 2);
    }
}
