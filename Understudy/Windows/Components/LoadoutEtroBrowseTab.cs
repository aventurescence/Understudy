using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Understudy.Models;

namespace Understudy.Windows.Components;

/// <summary>
/// "Browse Etro.gg" tab within the Loadout popup — search and import curated BiS sets.
/// </summary>
public class LoadoutEtroBrowseTab
{
    private readonly Plugin plugin;
    private readonly LoadoutPopupShared shared;
    private readonly Action closePopup;

    private List<EtroBiSSet> etroSets = new();
    private List<EtroBiSSet> filteredEtroSets = new();
    private bool isLoadingEtro = false;
    private string etroSearch = "";

    public LoadoutEtroBrowseTab(Plugin plugin, LoadoutPopupShared shared, Action closePopup)
    {
        this.plugin = plugin;
        this.shared = shared;
        this.closePopup = closePopup;
    }

    public void RefreshEtroList()
    {
        if (shared.ManualJobId == 0) return;

        isLoadingEtro = true;
        Task.Run(async () =>
        {
            try
            {
                etroSets = await plugin.EtroBrowseManager.GetEtroSets(shared.ManualJobId);
                FilterEtroSets();
            }
            finally
            {
                isLoadingEtro = false;
            }
        });
    }

    private void FilterEtroSets()
    {
        if (string.IsNullOrWhiteSpace(etroSearch))
        {
            filteredEtroSets = etroSets.ToList();
        }
        else
        {
            var term = etroSearch.ToLower();
            filteredEtroSets = etroSets.Where(s => s.name.ToLower().Contains(term) || s.creator.ToLower().Contains(term)).ToList();
        }
    }

    public void Draw()
    {
        ImGui.Spacing();

        var dl = ImGui.GetWindowDrawList();

        // ── Search bar row ──
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 130);
        if (ImGui.InputTextWithHint("##etroSearch", "Search by name or creator...", ref etroSearch, 64))
        {
            FilterEtroSets();
        }
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.20f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.35f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.50f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentPrimary);
        if (ImGui.Button("Refresh", new Vector2(110, 0)))
        {
            RefreshEtroList();
        }
        ImGui.PopStyleColor(4);

        ImGui.Spacing();

        // ── Result count pill ──
        if (!isLoadingEtro && filteredEtroSets.Any())
        {
            var countText = $"{filteredEtroSets.Count} set{(filteredEtroSets.Count != 1 ? "s" : "")}";
            shared.DrawPill(dl, ImGui.GetCursorScreenPos(), countText, Theme.AccentSecondary);
            ImGui.Dummy(new Vector2(0, 22));
        }

        // ── Loading state ──
        if (isLoadingEtro)
        {
            shared.DrawLoadingIndicator("Fetching sets from Etro.gg...");
            return;
        }

        // ── Empty state ──
        if (!filteredEtroSets.Any())
        {
            ImGui.Spacing();
            shared.DrawEmptyState(shared.ManualJobId == 0 ? "Select a job above to browse Etro.gg sets." : "No sets found. Try adjusting your search.");
            return;
        }

        // ── Scrollable set list ──
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.RowRounding);
        if (ImGui.BeginChild("EtroList", new Vector2(0, 350), false))
        {
            foreach (var set in filteredEtroSets)
            {
                DrawEtroSetRow(set);
            }
            ImGui.EndChild();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    private void DrawEtroSetRow(EtroBiSSet set)
    {
        var dl = ImGui.GetWindowDrawList();
        var rowHeight = 38f;
        var contentWidth = ImGui.GetContentRegionAvail().X;

        ImGui.PushID(set.id);

        var startPos = ImGui.GetCursorScreenPos();
        var storage = ImGui.GetStateStorage();
        var storageId = ImGui.GetID("IsExpanded");
        bool isExpanded = storage.GetBool(storageId, false);

        if (ImGui.InvisibleButton($"##sel{set.id}", new Vector2(contentWidth, rowHeight)))
        {
            storage.SetBool(storageId, !isExpanded);
        }

        bool isHovered = ImGui.IsItemHovered();
        if (isHovered)
        {
            ImGui.SetTooltip("Click to view details");
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rowEnd = startPos + new Vector2(contentWidth, rowHeight);
        var rowBg = isHovered ? Theme.EtroRowHover : Theme.EtroRowBg;

        dl.AddRectFilled(startPos, rowEnd, ImGui.GetColorU32(rowBg), Theme.RowRounding);

        if (isHovered)
            dl.AddRectFilled(startPos, startPos + new Vector2(3, rowHeight), ImGui.GetColorU32(Theme.ProviderEtro), 2f);

        var nameText = set.name;
        var maxNameWidth = contentWidth * 0.4f;
        if (ImGui.CalcTextSize(nameText).X > maxNameWidth)
        {
            while (nameText.Length > 3 && ImGui.CalcTextSize(nameText + "...").X > maxNameWidth)
                nameText = nameText[..^1];
            nameText += "...";
        }
        dl.AddText(startPos + new Vector2(14, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(Theme.TextPrimary), nameText);

        // ── Compact pill layout: IL | GCD | Patch ──
        float pillX = startPos.X + 14 + maxNameWidth + 20;
        float pillY = startPos.Y + (rowHeight - 20) * 0.5f;

        var avgIl = set.AverageItemLevel;
        var ilText = $"IL {avgIl}";
        var ilColor = avgIl >= Theme.ILThresholdMax ? Theme.ILTierMax
                    : avgIl >= Theme.ILThresholdHigh ? Theme.AccentSuccess
                    : Theme.AccentWarning;
        shared.DrawPill(dl, new Vector2(pillX, pillY), ilText, ilColor);
        pillX += ImGui.CalcTextSize(ilText).X + 24;

        if (set.gcd > 0)
        {
            var gcdText = $"GCD {set.gcd:F2}";
            shared.DrawPill(dl, new Vector2(pillX, pillY), gcdText, Theme.AccentSecondary);
            pillX += ImGui.CalcTextSize(gcdText).X + 24;
        }

        if (set.patch > 0)
        {
            var patchText = $"Patch {set.patch:F1}";
            var patchSize = ImGui.CalcTextSize(patchText);
            var pillPad = 16f;
            var patchX = rowEnd.X - patchSize.X - pillPad - 28;
            shared.DrawPill(dl, new Vector2(patchX, pillY), patchText, Theme.TextSecondary);
        }

        var arrowIcon = isExpanded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight;
        var arrowSize = 14f;
        var arrowPos = startPos + new Vector2(contentWidth - arrowSize - 10, (rowHeight - arrowSize) * 0.5f);
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(UiBuilder.IconFont, arrowSize, arrowPos, ImGui.GetColorU32(Theme.TextDisabled), arrowIcon.ToIconString());
        ImGui.PopFont();

        if (isExpanded)
        {
            DrawExpandedDetail(set, contentWidth);
        }

        ImGui.PopID();
        ImGui.Dummy(new Vector2(0, 2));
    }

    private void DrawExpandedDetail(EtroBiSSet set, float contentWidth)
    {
        ImGui.Spacing();
        ImGui.Indent(12);

        plugin.EtroBrowseManager.FetchEtroGearsetDetail(set.id);
        var detail = plugin.EtroBrowseManager.GetCachedGearsetDetail(set.id);

        if (detail != null)
        {
            int[] leftSlots = { 0, 1, 2, 3, 4, 6, 7 };
            int[] rightSlots = { 8, 9, 10, 11, 12 };

            if (ImGui.BeginTable($"##etroGear{set.id}", 2, ImGuiTableFlags.None, new Vector2(contentWidth - 24, 0)))
            {
                ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                foreach (var slotId in leftSlots)
                {
                    if (detail.Items.TryGetValue(slotId, out var slot))
                    {
                        SharedDrawHelpers.DrawItemIcon(slot.ItemId, 20f);
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.TextSecondary, $"{slot.Name}");
                    }
                }

                ImGui.TableNextColumn();
                foreach (var slotId in rightSlots)
                {
                    if (detail.Items.TryGetValue(slotId, out var slot))
                    {
                        SharedDrawHelpers.DrawItemIcon(slot.ItemId, 20f);
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.TextSecondary, $"{slot.Name}");
                    }
                }

                if (detail.FoodId != 0)
                {
                    SharedDrawHelpers.DrawItemIcon(detail.FoodId, 20f);
                    ImGui.SameLine();
                    var foodSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                    if (foodSheet != null && foodSheet.TryGetRow(detail.FoodId, out var foodRow))
                        ImGui.TextColored(Theme.AccentPrimary, foodRow.Name.ToString());
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
        }
        else
        {
            ImGui.TextColored(Theme.TextDisabled, "Loading gear details...");
            ImGui.Spacing();
        }

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.2f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.4f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.6f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentPrimary);

        if (ImGui.Button("Import This Loadout Target", new Vector2(240, 32)))
        {
            plugin.BiSImportManager.ImportFromUrl($"https://etro.gg/gearset/{set.id}", shared.ManualJobId, shared.CurrentCharacterId);
            closePopup();
        }
        ImGui.PopStyleColor(4);

        ImGui.Spacing();
        ImGui.Unindent(12);
    }

    public void Dispose()
    {
        etroSets.Clear();
        filteredEtroSets.Clear();
    }
}
