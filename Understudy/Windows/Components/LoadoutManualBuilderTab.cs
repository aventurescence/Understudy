using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Understudy.Models;

namespace Understudy.Windows.Components;

/// <summary>
/// "Manual Builder" tab within the Loadout popup — build a BiS set slot by slot.
/// </summary>
public class LoadoutManualBuilderTab : IDisposable
{
    private readonly Plugin plugin;
    private readonly LoadoutPopupShared shared;

    private int manualMinIl = 710;
    private int manualMaxIl = 735;
    private int defaultMinIl = 710;
    private int defaultMaxIl = 735;
    private bool ilRangeInitialized = false;

    private int manualSlotId = 0;
    private List<Lumina.Excel.Sheets.Item> manualSearchResults = new();
    private int selectedItemIndex = -1;

    private List<Lumina.Excel.Sheets.Item> foodSearchResults = new();
    private bool foodListBuilt = false;

    private static readonly Dictionary<int, Vector4> SlotIconUVs = new()
    {
        { 0, new Vector4(0f, 0f, 0.25f, 0.23255815f) },
        { 1, new Vector4(0.75f, 0.25f, 1f, 0.4651163f) },
        { 2, new Vector4(0.25f, 0f, 0.5f, 0.23255815f) },
        { 3, new Vector4(0.5f, 0f, 0.75f, 0.23255815f) },
        { 4, new Vector4(0.75f, 0f, 1f, 0.23255815f) },
        { 6, new Vector4(0.25f, 0.25f, 0.5f, 0.4651163f) },
        { 7, new Vector4(0.5f, 0.25f, 0.75f, 0.4651163f) },
        { 8, new Vector4(0f, 0.5f, 0.25f, 0.6976744f) },
        { 9, new Vector4(0.25f, 0.5f, 0.5f, 0.6976744f) },
        { 10, new Vector4(0.5f, 0.5f, 0.75f, 0.6976744f) },
        { 11, new Vector4(0.75f, 0.5f, 1f, 0.6976744f) },
        { 12, new Vector4(0.75f, 0.5f, 1f, 0.6976744f) },
    };

    public LoadoutManualBuilderTab(Plugin plugin, LoadoutPopupShared shared)
    {
        this.plugin = plugin;
        this.shared = shared;
    }

    public void Draw()
    {
        ImGui.Spacing();
        InitializeIlRange();

        // ── IL Range ──
        shared.DrawSectionHeader("Item Level Range");
        ImGui.Indent(12);
        DrawIlRangeControls();
        ImGui.Unindent(12);

        ImGui.Spacing();
        shared.DrawAccentSeparator();
        ImGui.Spacing();

        // ── Slot Selection Grid ──
        shared.DrawSectionHeader("Gear Slot");
        ImGui.Indent(12);
        DrawSlotGrid();
        ImGui.Unindent(12);

        ImGui.Spacing();
        shared.DrawAccentSeparator();
        ImGui.Spacing();

        // ── Item Selector ──
        shared.DrawSectionHeader("Select Item", $"{SharedDrawHelpers.GetSlotName(manualSlotId)} slot");
        ImGui.Indent(12);
        DrawItemSelector();
        ImGui.Unindent(12);

        // Materia (if item exists)
        DrawMateriaSection();

        // Food selector
        DrawFoodSection();
    }

    // ── IL Range Controls ──
    private void DrawIlRangeControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 4));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);

        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("##ilMin", ref manualMinIl, 0, 0))
        {
            if (manualMinIl < 1) manualMinIl = 1;
            if (manualMinIl > manualMaxIl) manualMinIl = manualMaxIl;
            RebuildItemList();
        }
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextSecondary, "–");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("##ilMax", ref manualMaxIl, 0, 0))
        {
            if (manualMaxIl < manualMinIl) manualMaxIl = manualMinIl;
            RebuildItemList();
        }
        ImGui.SameLine(0, 12);
        if (manualMinIl != defaultMinIl || manualMaxIl != defaultMaxIl)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.15f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.30f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentPrimary);
            if (ImGui.Button("Reset", new Vector2(50, 0)))
            {
                manualMinIl = defaultMinIl;
                manualMaxIl = defaultMaxIl;
                RebuildItemList();
            }
            ImGui.PopStyleColor(3);
        }

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    // ── Item Selector ──
    private void DrawItemSelector()
    {
        if (shared.ManualJobId == 0)
        {
            ImGui.TextColored(Theme.AccentWarning, "Select a job above to see available items.");
            return;
        }

        if (manualSearchResults.Count == 0 && shared.ManualJobId > 0)
            RebuildItemList();

        var preview = selectedItemIndex >= 0 && selectedItemIndex < manualSearchResults.Count
            ? manualSearchResults[selectedItemIndex].Name.ToString()
            : "Select an item...";

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
        if (ImGui.BeginCombo("##itemSelector", preview))
        {
            for (int i = 0; i < manualSearchResults.Count; i++)
            {
                var item = manualSearchResults[i];
                var itemName = item.Name.ToString();
                var il = item.LevelItem.RowId;
                bool isSelected = selectedItemIndex == i;

                if (ImGui.Selectable($"{itemName}  (IL {il})##item{item.RowId}", isSelected))
                {
                    selectedItemIndex = i;
                    plugin.BiSImportManager.SetBiSItem(shared.ManualJobId, manualSlotId, item.RowId, itemName, il, null!, shared.CurrentCharacterId);
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);

        if (!manualSearchResults.Any())
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.TextDisabled, "No items found for this slot and job.");
        }
    }

    // ── Materia Section ──
    private void DrawMateriaSection()
    {
        if (!shared.CurrentCharacterId.HasValue) return;
        if (!plugin.Configuration.Characters.TryGetValue(shared.CurrentCharacterId.Value, out var data)) return;
        if (!data.BisSets.TryGetValue(shared.ManualJobId, out var bisSet)) return;
        if (!bisSet.Items.TryGetValue(manualSlotId, out var bisItem)) return;

        ImGui.Spacing();
        shared.DrawAccentSeparator();
        ImGui.Spacing();

        int materiaSlots = 5;
        var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (itemSheet != null && itemSheet.TryGetRow(bisItem.ItemId, out var itemRow))
        {
            materiaSlots = itemRow.MateriaSlotCount;
            if (itemRow.IsAdvancedMeldingPermitted)
                materiaSlots = 5;
        }

        shared.DrawSectionHeader($"Materia Melds — {bisItem.Name}");
        ImGui.Indent(12);
        DrawMateriaSelector(bisItem, materiaSlots);
        ImGui.Unindent(12);
    }

    // ── Food Section ──
    private void DrawFoodSection()
    {
        if (!shared.CurrentCharacterId.HasValue) return;
        if (!plugin.Configuration.Characters.TryGetValue(shared.CurrentCharacterId.Value, out var foodCharData)) return;

        ImGui.Spacing();
        shared.DrawAccentSeparator();
        ImGui.Spacing();

        shared.DrawSectionHeader("Food");
        ImGui.Indent(12);
        DrawFoodSelector(foodCharData);
        ImGui.Unindent(12);
    }

    private void DrawFoodSelector(CharacterData charData)
    {
        if (!foodListBuilt)
        {
            BuildFoodList();
            foodListBuilt = true;
        }

        charData.BisSets.TryGetValue(shared.ManualJobId, out var bisSet);
        uint currentFoodId = bisSet?.FoodId ?? 0;

        string preview = "None";
        if (currentFoodId != 0)
        {
            var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            if (sheet != null && sheet.TryGetRow(currentFoodId, out var foodRow))
                preview = foodRow.Name.ToString();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
        if (ImGui.BeginCombo("##foodSelector", preview))
        {
            if (ImGui.Selectable("None", currentFoodId == 0))
            {
                plugin.BiSImportManager.SetBiSFood(shared.ManualJobId, 0, shared.CurrentCharacterId);
            }

            foreach (var food in foodSearchResults)
            {
                var name = food.Name.ToString();
                var il = food.LevelItem.RowId;
                bool isSelected = food.RowId == currentFoodId;

                if (ImGui.Selectable($"{name}  (IL {il})##food{food.RowId}", isSelected))
                {
                    plugin.BiSImportManager.SetBiSFood(shared.ManualJobId, food.RowId, shared.CurrentCharacterId);
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void BuildFoodList()
    {
        foodSearchResults.Clear();
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        foreach (var item in sheet)
        {
            if (item.ItemUICategory.RowId != TierConfig.FoodCategoryId) continue;
            if (item.LevelItem.RowId < TierConfig.FoodMinItemLevel) continue;
            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains("Seafood Stew") && item.LevelItem.RowId < 610) continue;

            foodSearchResults.Add(item);
        }

        foodSearchResults = foodSearchResults.OrderByDescending(f => f.LevelItem.RowId).ThenBy(f => f.Name.ToString()).ToList();
    }

    private void InitializeIlRange()
    {
        if (ilRangeInitialized) return;
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        int maxIl = 0;
        foreach (var item in sheet)
        {
            if (item.EquipSlotCategory.Value.RowId != 0 && (int)item.LevelItem.RowId > maxIl)
                maxIl = (int)item.LevelItem.RowId;
        }

        if (maxIl > 0)
        {
            manualMaxIl = maxIl;
            manualMinIl = maxIl - 20;
            defaultMaxIl = maxIl;
            defaultMinIl = maxIl - 20;
        }
        ilRangeInitialized = true;
    }

    public void RebuildItemList()
    {
        manualSearchResults.Clear();
        selectedItemIndex = -1;
        if (shared.ManualJobId == 0) return;

        InitializeIlRange();

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        foreach (var item in sheet)
        {
            if (item.LevelItem.RowId < (uint)manualMinIl || item.LevelItem.RowId > (uint)manualMaxIl) continue;
            if (!IsItemForSlot(item, manualSlotId)) continue;
            if (!IsItemForJob(item, shared.ManualJobId)) continue;

            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            manualSearchResults.Add(item);
            if (manualSearchResults.Count >= 100) break;
        }
    }

    private bool IsItemForJob(Lumina.Excel.Sheets.Item item, uint jobId)
    {
        var catRow = item.ClassJobCategory.Value;
        if (catRow.RowId == 0) return false;

        var classJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        if (classJobSheet == null || !classJobSheet.TryGetRow(jobId, out var job)) return true;

        var abbr = job.Abbreviation.ToString();
        var prop = catRow.GetType().GetProperty(abbr);
        if (prop != null && prop.PropertyType == typeof(bool))
            return (bool)prop.GetValue(catRow)!;

        return true;
    }

    private bool IsItemForSlot(Lumina.Excel.Sheets.Item item, int slotId)
    {
        if (item.EquipSlotCategory.Value.RowId == 0) return false;
        var cat = item.EquipSlotCategory.Value;

        return slotId switch
        {
            0 => cat.MainHand == 1,
            1 => cat.OffHand == 1,
            2 => cat.Head == 1,
            3 => cat.Body == 1,
            4 => cat.Gloves == 1,
            6 => cat.Legs == 1,
            7 => cat.Feet == 1,
            8 => cat.Ears == 1,
            9 => cat.Neck == 1,
            10 => cat.Wrists == 1,
            11 or 12 => cat.FingerL == 1 || cat.FingerR == 1,
            _ => false
        };
    }

    private void DrawSlotGrid()
    {
        int[] slots = { 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12 };
        float btnSize = 40f;
        float btnH = 40f;

        var style = ImGui.GetStyle();
        var spacing = style.ItemSpacing.X;
        var windowVisibleX = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;

        foreach (var s in slots)
        {
            var label = SharedDrawHelpers.GetSlotName(s);
            bool isSelected = manualSlotId == s;

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.30f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.45f });
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.55f });
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.SlotBtnBg);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.SlotBtnHover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.BgCardHover);
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 0));
            bool clicked = ImGui.Button($"##slot_{s}", new Vector2(btnSize, btnH));
            ImGui.PopStyleColor();

            if (SlotIconUVs.TryGetValue(s, out var uv))
            {
                var tex = Plugin.TextureProvider.GetFromGame("ui/uld/ArmouryBoard_hr1.tex");
                if (tex.TryGetWrap(out var wrap, out _))
                {
                    var dl2 = ImGui.GetWindowDrawList();
                    var bMin = ImGui.GetItemRectMin();
                    var iconSz = new Vector2(btnSize - 10, btnH - 10);
                    var iconOff = (new Vector2(btnSize, btnH) - iconSz) * 0.5f;
                    var tint = isSelected
                        ? ImGui.GetColorU32(new Vector4(0.7f, 0.6f, 1f, 1f))
                        : ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 0.8f, 0.8f));
                    dl2.AddImage(wrap.Handle, bMin + iconOff, bMin + iconOff + iconSz,
                        new Vector2(uv.X, uv.Y), new Vector2(uv.Z, uv.W), tint);
                }
            }

            if (clicked)
            {
                manualSlotId = s;
                RebuildItemList();
            }

            if (isSelected)
            {
                var dl = ImGui.GetWindowDrawList();
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                dl.AddRectFilled(
                    new Vector2(min.X + 4, max.Y - 3),
                    new Vector2(max.X - 4, max.Y),
                    ImGui.GetColorU32(Theme.AccentPrimary),
                    2f);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(label);

            ImGui.PopStyleColor(3);

            float nextBtnX = ImGui.GetItemRectMax().X + spacing + btnSize;
            if (s < 12 && nextBtnX < windowVisibleX) ImGui.SameLine();
        }
    }

    private void DrawMateriaSelector(BiSItem item, int slotCount = 5)
    {
        var dl = ImGui.GetWindowDrawList();
        if (slotCount < 1) slotCount = 2;

        for (int i = 0; i < slotCount; i++)
        {
            var matId = item.Materia.Count > i ? item.Materia[i] : 0;
            ImGui.PushID(i);

            bool hasMat = matId > 0;
            var btnColor = hasMat ? Theme.AccentSecondary with { W = 0.20f } : Theme.SlotBtnBg;
            var btnHover = hasMat ? Theme.AccentSecondary with { W = 0.35f } : Theme.SlotBtnHover;
            var textColor = hasMat ? Theme.AccentSecondary : Theme.TextDisabled;

            ImGui.PushStyleColor(ImGuiCol.Button, btnColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, btnHover);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);

            var btnLabel = hasMat ? $"M{i+1}" : "+";
            if (ImGui.Button(btnLabel, new Vector2(36, 34)))
            {
                ImGui.OpenPopup("MateriaPicker");
            }
            ImGui.PopStyleColor(3);

            if (hasMat)
            {
                var max = ImGui.GetItemRectMax();
                dl.AddCircleFilled(new Vector2(max.X - 4, ImGui.GetItemRectMin().Y + 4), 3f, ImGui.GetColorU32(Theme.AccentSecondary));
            }

            if (ImGui.IsItemHovered() && hasMat) ImGui.SetTooltip($"Materia Slot {i+1} — ID: {matId}");

            if (ImGui.BeginPopup("MateriaPicker"))
            {
                ImGui.TextColored(Theme.AccentPrimary, $"Slot {i+1} Materia");
                ImGui.Separator();
                ImGui.Spacing();

                if (hasMat)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentDanger);
                    if (ImGui.Selectable("Clear")) UpdateMateria(item, i, 0);
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                var options = plugin.StatCalculator.GetMateriaOptions();
                int lastGrade = -1;
                foreach (var opt in options)
                {
                    if (opt.Grade != lastGrade)
                    {
                        if (lastGrade >= 0) ImGui.Spacing();
                        ImGui.TextColored(Theme.TextSecondary, $"Grade {ToRoman(opt.Grade + 1)}");
                        lastGrade = opt.Grade;
                    }
                    if (ImGui.Selectable($"  {opt.StatName} +{opt.Value}"))
                        UpdateMateria(item, i, opt.ItemId);
                }

                ImGui.EndPopup();
            }
            ImGui.PopID();
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void UpdateMateria(BiSItem item, int slotIndex, uint materiaId)
    {
        while (item.Materia.Count <= slotIndex) item.Materia.Add(0);
        item.Materia[slotIndex] = materiaId;
        plugin.BiSImportManager.SetBiSItem(shared.ManualJobId, item.Slot, item.ItemId, item.Name, item.ItemLevel, item.Materia, shared.CurrentCharacterId);
    }

    private static string ToRoman(int n) => n switch
    {
        >= 10 => "X" + ToRoman(n - 10),
        9 => "IX" + ToRoman(n - 9),
        >= 5 => "V" + ToRoman(n - 5),
        4 => "IV" + ToRoman(n - 4),
        >= 1 => "I" + ToRoman(n - 1),
        _ => ""
    };

    public void Dispose()
    {
        manualSearchResults.Clear();
    }
}
