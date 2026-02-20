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
                    ImGui.SameLine();
                    materiaDisplay.DrawMateriaRow(comp.CurrentItem.Materia, comp.BiSItem?.Materia);
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
