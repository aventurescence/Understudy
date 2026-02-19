using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Understudy.Managers;

namespace Understudy.Windows.Components;

public class LoadoutPopup : IDisposable
{
    private readonly Plugin plugin;
    private bool isOpen = false;
    private ulong? currentCharacterId;
    
    // Popup state
    private string importUrl = "";
    private uint selectedJobId = 0; // For URL import override
    private bool shouldOpen = false;
    
    // Manual entry state
    private int manualMinIl = 710;
    private int manualMaxIl = 735;
    private int manualSlotId = 0;
    private uint manualJobId = 0;
    private string manualSearch = "";
    private List<Lumina.Excel.Sheets.Item> manualSearchResults = new();

    // Etro Browse State
    private List<EtroBiSSet> etroSets = new();
    private List<EtroBiSSet> filteredEtroSets = new();
    private bool isLoadingEtro = false;
    private string etroSearch = "";

    public LoadoutPopup(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Open(ulong characterId)
    {
        currentCharacterId = characterId;
        isOpen = true;
        
        // Auto-detect current job
        if (Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc)
        {
            if (!plugin.IsJobExcluded(pc.ClassJob.RowId))
            {
                selectedJobId = pc.ClassJob.RowId;
                manualJobId = pc.ClassJob.RowId;
            }
            else
            {
                selectedJobId = 0;
                manualJobId = 0;
            }
        }
        else
        {
            selectedJobId = 0;
            manualJobId = 0;
        }
        
        // Refresh Etro list if needed
        RefreshEtroList();

        shouldOpen = true;
    }

    private void RefreshEtroList()
    {
        if (manualJobId == 0) return;
        
        isLoadingEtro = true;
        Task.Run(async () =>
        {
            try
            {
                etroSets = await plugin.BiSManager.GetEtroSets(manualJobId);
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
        if (shouldOpen)
        {
            ImGui.OpenPopup("Manage Loadout");
            shouldOpen = false;
        }

        if (!isOpen) return;

        // Custom style for popup
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(20, 20));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 12f);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Theme.BgPopup);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Theme.BgDark);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Theme.BgDark);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.AccentPrimary);

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(700, 600), ImGuiCond.FirstUseEver);

        if (ImGui.BeginPopupModal("Manage Loadout", ref isOpen, ImGuiWindowFlags.NoResize))
        {
            // Header
            DrawJobSelector(ref manualJobId, "Loadout Job");
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.BeginTabBar("LoadoutTabs", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("  Track Configuration  "))
                {
                    DrawTrackConfigTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("  Browse Etro.gg  "))
                {
                    DrawEtroBrowseTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("  Manual Builder  "))
                {
                    DrawManualEntry();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("  Import URL  "))
                {
                    DrawUrlImport();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            // Footer Spacer
            var availY = ImGui.GetContentRegionAvail().Y;
            if (availY > 40) ImGui.Dummy(new Vector2(0, availY - 40));

            ImGui.Separator();
            ImGui.Spacing();

            // Footer Buttons
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 120);
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgCard);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);
            if (ImGui.Button("Close", new Vector2(100, 30)))
            {
                isOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor(2);

            ImGui.EndPopup();
        }
        else
        {
            isOpen = false;
        }

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
    }

    private void DrawTrackConfigTab()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.AccentPrimary, "1. Gear Snapshot");
        ImGui.Indent(10);
        ImGui.TextColored(Theme.TextSecondary, "Snapshot your currently equipped gear to track item level and upgrades.");
        
        ImGui.Spacing();
        
        bool isCurrentJob = Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc && pc.ClassJob.RowId == manualJobId;
        
        if (isCurrentJob)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentSuccess);
            if (ImGui.Button("Snapshot Current Gear", new Vector2(250, 35)))
            {
                plugin.TrackCurrentGearset();
                // We don't close here, allow them to set BiS
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.TextColored(Theme.AccentWarning, "Switch to this job in-game to snapshot gear.");
        }
        
        ImGui.Unindent(10);
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        ImGui.TextColored(Theme.AccentPrimary, "2. BiS Target (Optional)");
        ImGui.Indent(10);
        ImGui.TextColored(Theme.TextSecondary, "Select a Best-in-Slot target to compare against.");
        
        ImGui.TextColored(Theme.TextDisabled, "Use the Browse Etro.gg or Import URL tabs above to set a BiS target.");
        
        ImGui.Spacing();
        ImGui.Text("Current BiS Status:");
        if (currentCharacterId.HasValue && plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var charData) && charData.BisSets.TryGetValue(manualJobId, out var bis))
        {
            ImGui.TextColored(Theme.AccentSuccess, $"{bis.Name} ({bis.SourceType})");
            if (ImGui.SmallButton("Clear BiS Target"))
            {
                charData.BisSets.Remove(manualJobId);
                plugin.Configuration.Save();
            }
        }
        else
        {
            ImGui.TextColored(Theme.TextDisabled, "None (Tracking only)");
        }
        
        ImGui.Unindent(10);
    }

    private void DrawEtroBrowseTab()
    {
        ImGui.Spacing();
        
        // Search bar
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 120);
        if (ImGui.InputTextWithHint("##etroSearch", "Search sets...", ref etroSearch, 64))
        {
            FilterEtroSets();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Refresh", new Vector2(100, 0)))
        {
            RefreshEtroList();
        }
        
        ImGui.Spacing();
        
        if (isLoadingEtro)
        {
            ImGui.TextColored(Theme.AccentPrimary, "Loading sets from Etro.gg...");
            return;
        }
        
        if (!filteredEtroSets.Any())
        {
            ImGui.TextColored(Theme.TextDisabled, "No sets found for this job.");
            return;
        }
        
        // List/Table
        if (ImGui.BeginChild("EtroList", new Vector2(0, 300), true))
        {
            foreach (var set in filteredEtroSets)
            {
                ImGui.PushID(set.id);
                if (ImGui.Selectable($"##sel{set.id}", false, ImGuiSelectableFlags.None, new Vector2(0, 50)))
                {
                    // Import immediately on click? Or select?
                    // Let's import immediately.
                    plugin.BiSManager.ImportFromUrl($"https://etro.gg/gearset/{set.id}", manualJobId);
                    isOpen = false; // Close on success? 
                    ImGui.CloseCurrentPopup();
                }
                
                if (ImGui.IsItemHovered())
                {
                     ImGui.SetTooltip("Click to Import");
                     ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                // Render content inside the selectable rect
                var min = ImGui.GetItemRectMin();
                var dl = ImGui.GetWindowDrawList();
                
                // Name
                dl.AddText(min + new Vector2(10, 5), ImGui.GetColorU32(Theme.TextPrimary), set.name);
                
                // Details
                string details = $"IL {set.minItemLevel}-{set.maxItemLevel} | by {set.creator}";
                dl.AddText(min + new Vector2(10, 25), ImGui.GetColorU32(Theme.TextSecondary), details);
                
                // Date
                string date = set.userUpdatedAt.ToString("yyyy-MM-dd");
                var dateSize = ImGui.CalcTextSize(date);
                dl.AddText(min + new Vector2(ImGui.GetItemRectSize().X - dateSize.X - 10, 15), ImGui.GetColorU32(Theme.TextDisabled), date);

                ImGui.PopID();
            }
            ImGui.EndChild();
        }
    }

    private void DrawUrlImport()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.AccentPrimary, "Paste Link");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 10);
        ImGui.InputTextWithHint("##importUrl", "https://etro.gg/gearset/...", ref importUrl, 512);
        
        ImGui.Spacing();
        ImGui.TextColored(Theme.TextSecondary, "Supports Etro.gg and XIVGear.app links.");
        
        ImGui.Spacing();
        bool canImport = !string.IsNullOrWhiteSpace(importUrl) && manualJobId != 0;
        
        if (!canImport) ImGui.BeginDisabled();
        
        if (ImGui.Button("Import Gearset", new Vector2(160, 35)))
        {
             if (currentCharacterId.HasValue && plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var data))
             {
                 plugin.BiSManager.ImportFromUrl(importUrl, manualJobId);
                 isOpen = false; 
                 ImGui.CloseCurrentPopup();
             }
        }
        
        if (!canImport) ImGui.EndDisabled();
    }

    private void DrawManualEntry()
    {
        ImGui.Spacing();
        
        // 1. Selector Row
        ImGui.BeginGroup();
        // Job selector is global now in header
        
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Min IL", ref manualMinIl);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Max IL", ref manualMaxIl);
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 2. Slot Selection
        DrawSlotGrid();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 3. Search
        ImGui.TextColored(Theme.AccentPrimary, "Search Item");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100);
        ImGui.InputText("##search", ref manualSearch, 64);
        ImGui.SameLine();
        if (ImGui.Button("Search", new Vector2(90, 0)))
        {
            SearchItems();
        }

        // Results
        if (manualSearchResults.Any())
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextColored(Theme.TextSecondary, "Results:");
            
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.BgDark);
            if (ImGui.BeginChild("SearchResults", new Vector2(0, 150), true))
            {
                foreach (var item in manualSearchResults)
                {
                    string label = $"{item.Name} (IL {item.LevelItem.RowId})";
                    if (ImGui.Selectable(label))
                    {
                        plugin.BiSManager.SetBiSItem(manualJobId, manualSlotId, item.RowId, item.Name.ToString(), item.LevelItem.RowId, null!);
                    }
                }
                ImGui.EndChild();
            }
            ImGui.PopStyleColor();
        }

        // 4. Materia (if item exists)
        if (currentCharacterId.HasValue &&
            plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var data) &&
            data.BisSets.TryGetValue(manualJobId, out var bisSet) && 
            bisSet.Items.TryGetValue(manualSlotId, out var bisItem))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            ImGui.TextColored(Theme.AccentSecondary, $"Melds for {bisItem.Name}:");
            DrawMateriaSelector(bisItem);
        }
    }

    private void SearchItems()
    {
        manualSearchResults.Clear();
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        var searchLower = manualSearch.ToLower();

        foreach (var item in sheet)
        {
            if (item.LevelItem.RowId < manualMinIl || item.LevelItem.RowId > manualMaxIl) continue;

            bool slotMatch = IsItemForSlot(item, manualSlotId);
            if (!slotMatch) continue;

            if (!string.IsNullOrEmpty(searchLower) && !item.Name.ToString().ToLower().Contains(searchLower))
                continue;

            manualSearchResults.Add(item);
            if (manualSearchResults.Count >= 50) break;
        }
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
        float w = 45;
        float h = 30;

        ImGui.TextColored(Theme.TextSecondary, "Select Slot:");
        
        var style = ImGui.GetStyle();
        var spacing = style.ItemSpacing.X;
        var windowVisibleX = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;

        foreach (var s in slots)
        {
            var label = s switch { 0=>"Wep", 1=>"Off", 2=>"Head", 3=>"Body", 4=>"Hand", 6=>"Legs", 7=>"Feet", 8=>"Ears", 9=>"Neck", 10=>"Wris", 11=>"Rg R", 12=>"Rg L", _=>"?" };
            
            bool isSelected = manualSlotId == s;
            if (isSelected) 
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W=0.9f });
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgCard);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);
            }

            if (ImGui.Button(label, new Vector2(w, h)))
            {
                manualSlotId = s;
                manualSearchResults.Clear();
            }
            
            ImGui.PopStyleColor(2);

            float nextBtnX = ImGui.GetItemRectMax().X + spacing + w;
            if (s < 12 && nextBtnX < windowVisibleX) ImGui.SameLine();
        }
    }
    
    private void DrawJobSelector(ref uint jobId, string label)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        var currentJob = sheet?.GetRow(jobId);
        var preview = currentJob != null ? $"{currentJob.Value.Abbreviation} - {currentJob.Value.Name}" : "Select Job...";

        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo($"##{label}Selector", preview))
        {
            if (sheet != null)
            {
                foreach (var job in sheet)
                {
                     if (job.RowId == 0 || job.Abbreviation.ExtractText().IsNullOrEmpty()) continue;
                     if (plugin.IsJobExcluded(job.RowId)) continue;
                     
                     bool isSelected = jobId == job.RowId;
                     if (ImGui.Selectable($"{job.Abbreviation} - {job.Name}", isSelected))
                     {
                         jobId = job.RowId;
                         // On job change, refresh Etro list
                         RefreshEtroList();
                     }
                     if (isSelected) ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
    }
    
    private void DrawMateriaSelector(BiSItem item)
    {
        for (int i = 0; i < 5; i++)
        {
            var matId = item.Materia.Count > i ? item.Materia[i] : 0;
            ImGui.PushID(i);
            
            ImGui.PushStyleColor(ImGuiCol.Button, matId > 0 ? Theme.BgCardHover : Theme.BgCard);
            if (ImGui.Button(matId == 0 ? "+" : "M", new Vector2(30, 30)))
            {
                ImGui.OpenPopup("MateriaPicker");
            }
            ImGui.PopStyleColor();
            
            if (ImGui.IsItemHovered() && matId > 0) ImGui.SetTooltip($"Materia ID: {matId}");

            if (ImGui.BeginPopup("MateriaPicker"))
            {
                if (ImGui.Selectable("Clear")) UpdateMateria(item, i, 0);
                ImGui.Separator();
                if (ImGui.Selectable("Crit XII")) UpdateMateria(item, i, 44103);
                if (ImGui.Selectable("Det XII")) UpdateMateria(item, i, 44104);
                if (ImGui.Selectable("DH XII")) UpdateMateria(item, i, 44105);
                if (ImGui.Selectable("SpS XII")) UpdateMateria(item, i, 44106);
                if (ImGui.Selectable("SkS XII")) UpdateMateria(item, i, 44107);
                ImGui.Separator();
                if (ImGui.Selectable("Crit XI")) UpdateMateria(item, i, 44098);
                if (ImGui.Selectable("Det XI")) UpdateMateria(item, i, 44099);
                if (ImGui.Selectable("DH XI")) UpdateMateria(item, i, 44100);
                if (ImGui.Selectable("SpS XI")) UpdateMateria(item, i, 44101);
                if (ImGui.Selectable("SkS XI")) UpdateMateria(item, i, 44102);

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
        plugin.BiSManager.SetBiSItem(manualJobId, item.Slot, item.ItemId, item.Name, item.ItemLevel, item.Materia);
    }

    public void Dispose()
    {
        // Clear large lists to help GC? Not strictly necessary but good practice.
        manualSearchResults.Clear();
        etroSets.Clear();
        filteredEtroSets.Clear();
    }
}
