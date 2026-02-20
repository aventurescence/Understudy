using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Understudy.Managers;

public class BiSManager
{
    private class EtroFoodItem { public uint id { get; set; } public uint item { get; set; } }
    private Dictionary<uint, uint>? cachedEtroFoodMap = null;
    private readonly Plugin plugin;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    
    // Regex patterns for URLs
    private static readonly Regex EtroPattern = new(@"https?://etro\.gg/gearset/([^/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex XivGearPattern = new(@"(?:https?://xivgear\.app/\?page=sl\||https?://api\.xivgear\.app/shortlink/)([a-zA-Z0-9-]+)(?:&(?:selectedIndex|onlySetIndex)=(\d+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsLoading { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public BiSManager(Plugin plugin, IDataManager dataManager, IPluginLog log)
    {
        this.plugin = plugin;
        this.dataManager = dataManager;
        this.log = log;
    }

    public void ImportFromUrl(string url, uint jobId)
    {
        // Fire and forget task
        Task.Run(async () => await ImportAsync(url, jobId));
    }

    private async Task ImportAsync(string url, uint jobId)
    {
        IsLoading = true;
        LastError = string.Empty;

        if (plugin.IsJobExcluded(jobId))
        {
            log.Warning($"Attempted to import BiS for excluded job {jobId}");
            LastError = "This job is excluded from tracking.";
            IsLoading = false;
            return;
        }
        
        try
        {
            log.Information($"Starting BiS import for job {jobId} from URL: {url}");
            var etroMatch = EtroPattern.Match(url);
            if (etroMatch.Success)
            {
                await ImportEtro(etroMatch.Groups[1].Value, jobId, url);
                plugin.CheckAndTrackGear(jobId);
                return;
            }

            var xivMatch = XivGearPattern.Match(url);
            if (xivMatch.Success)
            {
                await ImportXivGear(xivMatch.Groups[1].Value, xivMatch.Groups[2].Value, jobId, url);
                plugin.CheckAndTrackGear(jobId);
                return;
            }
            
            LastError = "Invalid URL format. Please use a valid etro.gg or xivgear.app link.";
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to import BiS");
            LastError = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Etro.gg Import ─────────────────────────────────────────────
    
    private class EtroResponse
    {
        public string? name { get; set; }
        public uint? weapon { get; set; }
        public uint? head { get; set; }
        public uint? body { get; set; }
        public uint? hands { get; set; }
        public uint? legs { get; set; }
        public uint? feet { get; set; }
        public uint? offHand { get; set; }
        public uint? ears { get; set; }
        public uint? neck { get; set; }
        public uint? wrists { get; set; }
        public uint? fingerL { get; set; }
        public uint? fingerR { get; set; }
        public uint? food { get; set; }
        public Dictionary<string, Dictionary<string, uint>>? materia { get; set; }
    }

    private async Task ImportEtro(string id, uint jobId, string originalUrl)
    {
        var response = await plugin.HttpClient.GetStringAsync($"https://etro.gg/api/gearsets/{id}");
        var data = System.Text.Json.JsonSerializer.Deserialize<EtroResponse>(response);
        if (data == null) throw new Exception("Empty response from Etro");

        uint resolvedFoodId = 0;
        if (data.food.HasValue && data.food.Value > 0)
        {
            try
            {
                if (cachedEtroFoodMap == null)
                {
                    var foodJson = await plugin.HttpClient.GetStringAsync("https://etro.gg/api/food/");
                    var foodList = System.Text.Json.JsonSerializer.Deserialize<List<EtroFoodItem>>(foodJson);
                    if (foodList != null)
                        cachedEtroFoodMap = foodList.ToDictionary(f => f.id, f => f.item);
                }
                
                if (cachedEtroFoodMap != null && cachedEtroFoodMap.TryGetValue(data.food.Value, out var mappedItem))
                {
                    resolvedFoodId = mappedItem;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"[Understudy] Failed to fetch Etro food mapping for id {data.food}");
            }
        }

        var bis = new BiSData
        {
            JobId = jobId,
            SourceType = "etro",
            SourceUrl = originalUrl,
            Name = data.name ?? string.Empty,
            LastUpdated = DateTime.UtcNow,
            FoodId = resolvedFoodId
        };

        void AddSlot(int slotId, uint? itemId, string? slotKeyOverride = null)
        {
            if (itemId == null || itemId == 0) return;
            var item = CreateBiSItem((uint)itemId, slotId);
            
            // Materia
            string key = itemId.ToString()!;
            string keyOverride = slotKeyOverride ?? key;
            
            if (data.materia != null)
            {
                Plugin.Log.Debug($"[Understudy] Etro: Looking for materia. SlotId: {slotId}, ItemId: {itemId}, KeyOverride: {keyOverride}");
                if (data.materia.TryGetValue(keyOverride, out var slots) ||
                    data.materia.TryGetValue(key, out slots) ||
                    data.materia.TryGetValue(key + "R", out slots) ||
                    data.materia.TryGetValue(key + "L", out slots))
                {
                    Plugin.Log.Debug($"[Understudy] Etro: Found materia for slot {slotId}.");
                    for (int i = 1; i <= 5; i++)
                    {
                        if (slots.TryGetValue(i.ToString(), out var matId))
                            item.Materia[i - 1] = matId;
                    }
                }
                else
                {
                    Plugin.Log.Warning($"[Understudy] Etro: NO materia found for slot {slotId}. Tried keys: {keyOverride}, {key}, {key}R, {key}L");
                }
            }
            
            bis.Items[slotId] = item;
        }

        AddSlot(0, data.weapon);
        AddSlot(1, data.offHand);
        AddSlot(2, data.head);
        AddSlot(3, data.body);
        AddSlot(4, data.hands);
        AddSlot(6, data.legs);
        AddSlot(7, data.feet);
        AddSlot(8, data.ears);
        AddSlot(9, data.neck);
        AddSlot(10, data.wrists);
        AddSlot(11, data.fingerR, data.fingerR + "R");
        AddSlot(12, data.fingerL, data.fingerL + "L");

        SaveBiS(jobId, bis);
    }

    // ── XIVGear.app Import ─────────────────────────────────────────

    private class XGSetCollection { public string? name { get; set; } public List<XGSet>? sets { get; set; } }
    private class XGSet { public string? name { get; set; } public Dictionary<string, XGItem>? items { get; set; } }
    private class XGItem { public uint id { get; set; } public List<XGMateria>? materia { get; set; } }
    private class XGMateria { public int id { get; set; } }

    private async Task ImportXivGear(string shortcode, string setIndexStr, uint jobId, string originalUrl)
    {
        var json = await plugin.HttpClient.GetStringAsync($"https://api.xivgear.app/shortlink/{shortcode}");
        
        XGSet? targetSet = null;

        // XIVGear can return either a collection of sets or a single set.
        // Try collection first — if it has a valid sets array, pick the requested index.
        var coll = System.Text.Json.JsonSerializer.Deserialize<XGSetCollection>(json);
        if (coll?.sets != null && coll.sets.Count > 0)
        {
            int idx = int.TryParse(setIndexStr, out var i) ? i : 0;
            if (idx < coll.sets.Count)
                targetSet = coll.sets[idx];
            else
                throw new Exception($"Set index {idx} is out of range (collection has {coll.sets.Count} sets)");
        }

        // If no collection match, parse as a single set
        if (targetSet == null)
        {
            targetSet = System.Text.Json.JsonSerializer.Deserialize<XGSet>(json);
        }

        if (targetSet == null) throw new Exception("Failed to parse XIVGear response");

        var bis = new BiSData
        {
            JobId = jobId,
            SourceType = "xivgear",
            SourceUrl = originalUrl,
            Name = targetSet.name ?? string.Empty,
            LastUpdated = DateTime.UtcNow
        };

        void AddSlot(int slotId, string key)
        {
            if (targetSet.items != null && targetSet.items.TryGetValue(key, out var xItem))
            {
                var item = CreateBiSItem(xItem.id, slotId);
                
                // Materia
                if (xItem.materia != null)
                {
                    for (int i = 0; i < Math.Min(5, xItem.materia.Count); i++)
                    {
                        var mId = xItem.materia[i].id;
                        if (mId > 0) item.Materia[i] = (uint)mId;
                    }
                }
                
                bis.Items[slotId] = item;
            }
        }

        AddSlot(0, "Weapon");
        AddSlot(1, "OffHand");
        AddSlot(2, "Head");
        AddSlot(3, "Body");
        AddSlot(4, "Hand");
        AddSlot(6, "Legs");
        AddSlot(7, "Feet");
        AddSlot(8, "Ears");
        AddSlot(9, "Neck");
        AddSlot(10, "Wrist");
        AddSlot(11, "RingRight");
        AddSlot(12, "RingLeft");

        SaveBiS(jobId, bis);
    }

    // ── Helper Logic ───────────────────────────────────────────────

    private BiSItem CreateBiSItem(uint itemId, int slot)
    {
        var sheet = dataManager.GetExcelSheet<Item>();
        if (sheet != null && sheet.TryGetRow(itemId, out var row))
        {
            var name = row.Name.ToString();
            var il = row.LevelItem.RowId;
            var source = ClassifySource(name, il, slot);
            
            return new BiSItem
            {
                ItemId = itemId,
                Name = name,
                ItemLevel = il,
                Slot = slot,
                Source = source,
                FloorSource = source == GearSource.Savage ? GetSavageFloor(slot) : string.Empty
            };
        }
        
        return new BiSItem { ItemId = itemId, Slot = slot, Name = "Unknown Item" };
    }

    private GearSource ClassifySource(string name, uint il, int slot)
    {
        if (name.Contains(TierConfig.SavageGearPrefix)) return GearSource.Savage;
        if (name.Contains(TierConfig.AugmentedTomeGearPrefix)) return GearSource.AugmentedTomestone;
        if (name.Contains(TierConfig.BaseTomeGearPrefix)) return GearSource.Tomestone;

        return GearSource.Unknown;
    }

    private string GetSavageFloor(int slot)
    {
        return slot switch
        {
            0 => "M4", // Weapon
            2 or 4 or 7 => "M2", // Head, Hands, Feet
            3 or 6 => "M3", // Body, Legs
            8 or 9 or 10 or 11 or 12 => "M1", // Accessories
            _ => ""
        };
    }

    public string GetSourceAcquisition(BiSItem item, ref AcquisitionCosts costs)
    {
        switch (item.Source)
        {
            case GearSource.Savage:
                // Needs Book and potentially Twine/Glaze/Solvent later if augmented
                string floor = GetSavageFloor(item.Slot);
                if (costs.BooksNeeded.ContainsKey(floor))
                    costs.BooksNeeded[floor]++;
                else
                    costs.BooksNeeded[floor] = 1;

                return $"{floor} Drop / Book";

            case GearSource.AugmentedTomestone:
                // Needs Base Tome + Upgrade Material
                // Base Cost
                int tomes = item.Slot switch
                {
                    0 => 500, // Weapon (augmented)
                    3 or 6 => 825, // Body/Legs
                    2 or 4 or 7 => 495, // Head/Hands/Feet
                    _ => 375 // Accessories
                };
                costs.TomestonesNeeded += tomes;

                // Weapon also needs Universal Tomestone 3.0
                if (item.Slot == 0) costs.UniversalTomestonesNeeded += 1;

                // Material
                if (item.Slot == 0) // Weapon
                    costs.SolventNeeded++;
                else if (new[] { 2, 3, 4, 6, 7 }.Contains(item.Slot)) // Left side
                    costs.TwineNeeded++;
                else // Right side
                    costs.GlazeNeeded++;

                return "Tomes + Upgrade";

            case GearSource.Tomestone:
                // Unaugmented
                int baseTomes = item.Slot switch
                {
                    0 => 500,
                    3 or 6 => 825,
                    2 or 4 or 7 => 495,
                    _ => 375
                };
                costs.TomestonesNeeded += baseTomes;
                if (item.Slot == 0) costs.UniversalTomestonesNeeded += 1;
                return $"{baseTomes} Tomes";
        }

        return "Unknown";
    }

    public uint ResolveEtroFoodId(uint etroFoodId)
    {
        if (cachedEtroFoodMap != null && cachedEtroFoodMap.TryGetValue(etroFoodId, out var ffxivId))
            return ffxivId;
        return 0;
    }

    private void SaveBiS(uint jobId, BiSData bis)
    {
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0) return;

        if (plugin.IsJobExcluded(jobId)) return;
        
        if (plugin.Configuration.Characters.TryGetValue(contentId, out var charData))
        {
            charData.BisSets[jobId] = bis;
            plugin.Configuration.Save();
        }
    }

    // ── Comparison Logic ───────────────────────────────────────────

    public (List<BiSSlotComparison> Comparisons, AcquisitionCosts Costs) Compare(uint jobId)
    {
        var costs = new AcquisitionCosts();
        var comparisons = new List<BiSSlotComparison>();
        
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0 || !plugin.Configuration.Characters.TryGetValue(contentId, out var charData))
            return (comparisons, costs);

        if (!charData.BisSets.TryGetValue(jobId, out var bisData))
            return (comparisons, costs);

        var currentGear = charData.GearSets.TryGetValue(jobId, out var set) ? set : new GearSetData();

        // Check each slot in the BiS set
        foreach (var kvp in bisData.Items.OrderBy(k => k.Key))
        {
            var slot = kvp.Key;
            var bisItem = kvp.Value;
            var currentItem = currentGear.Items.FirstOrDefault(x => x.Slot == slot);
            
            var isOwned = currentItem != null && currentItem.ItemId == bisItem.ItemId;

            if (!isOwned && (slot == 11 || slot == 12))
            {
                var oppositeSlot = slot == 11 ? 12 : 11;
                var oppositeItem = currentGear.Items.FirstOrDefault(x => x.Slot == oppositeSlot);
                if (oppositeItem != null && oppositeItem.ItemId == bisItem.ItemId)
                {
                    isOwned = true;
                    currentItem = oppositeItem;
                }
            }
            
            var label = "";

            if (!isOwned)
            {
                // Calculate costs
                label = GetCostLabel(bisItem, ref costs);
            }

            comparisons.Add(new BiSSlotComparison
            {
                SlotId = slot,
                BiSItem = bisItem,
                CurrentItem = currentItem,
                IsOwned = isOwned,
                AcquisitionLabel = label
            });
        }

        return (comparisons, costs);
    }

    private string GetCostLabel(BiSItem item, ref AcquisitionCosts costs)
    {
        // If we already have the base item but need augment?
        // Detailed logic is complex because we don't know if the user has the base item unless we scan inventory.
        // For now, assume "From Scratch" cost.

        switch (item.Source)
        {
            case GearSource.Savage:
                // M1-M4 drop
                if (!string.IsNullOrEmpty(item.FloorSource))
                {
                    // Book cost?
                    int bookCost = item.Slot switch
                    {
                        0 => 8, // Weapon
                        3 or 6 => 6, // Body/Legs
                        2 or 4 or 7 => 4, // Head/Hands/Feet
                        _ => 3 // Accessories
                    };
                    
                    // Don't double count books if we are buying distinct items? 
                    // Actually we aggregate total books needed.
                    costs.BooksNeeded[item.FloorSource] += bookCost;
                    return $"{item.FloorSource} Savage";
                }
                break;

            case GearSource.AugmentedTomestone:
                // Needs Base Tome + Upgrade Material
                // Base Cost
                int tomes = item.Slot switch
                {
                    0 => 500, // Weapon (augmented)
                    3 or 6 => 825, // Body/Legs
                    2 or 4 or 7 => 495, // Head/Hands/Feet
                    _ => 375 // Accessories
                };
                costs.TomestonesNeeded += tomes;

                // Weapon also needs Universal Tomestone 3.0
                if (item.Slot == 0) costs.UniversalTomestonesNeeded += 1;

                // Material
                if (item.Slot == 0) // Weapon
                    costs.SolventNeeded++;
                else if (new[] { 2, 3, 4, 6, 7 }.Contains(item.Slot)) // Left side
                    costs.TwineNeeded++;
                else // Right side
                    costs.GlazeNeeded++;

                return "Tomes + Upgrade";

            case GearSource.Tomestone:
                // Unaugmented
                int baseTomes = item.Slot switch
                {
                    0 => 500,
                    3 or 6 => 825,
                    2 or 4 or 7 => 495,
                    _ => 375
                };
                costs.TomestonesNeeded += baseTomes;
                if (item.Slot == 0) costs.UniversalTomestonesNeeded += 1;
                return $"{baseTomes} Tomes";
        }

        return "Unknown";
    }
    public void SetBiSItem(uint jobId, int slotId, uint itemId, string name, uint itemLevel, List<uint> materia)
    {
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0) return;

        if (plugin.IsJobExcluded(jobId)) return;

        if (!plugin.Configuration.Characters.TryGetValue(contentId, out var charData))
            return;

        if (!charData.BisSets.TryGetValue(jobId, out var set))
        {
            set = new BiSData { JobId = jobId, SourceType = "manual", Name = "Manual Set" };
            charData.BisSets[jobId] = set;
        }

        if (itemId == 0)
        {
            set.Items.Remove(slotId);
        }
        else
        {
            var newItem = new BiSItem
            {
                ItemId = itemId,
                Name = name,
                ItemLevel = itemLevel,
                Slot = slotId,
                Materia = materia ?? new List<uint> { 0, 0, 0, 0, 0 }
            };
            
            ClassifyItem(newItem);
            
            set.Items[slotId] = newItem;
        }
        
        plugin.Configuration.Save();
    }

    private void ClassifyItem(BiSItem item)
    {
        item.Source = ClassifySource(item.Name, item.ItemLevel, item.Slot);
        item.FloorSource = item.Source == GearSource.Savage ? GetSavageFloor(item.Slot) : string.Empty;
    }

    public void SetBiSFood(uint jobId, uint foodItemId)
    {
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0) return;

        if (!plugin.Configuration.Characters.TryGetValue(contentId, out var charData))
            return;

        if (!charData.BisSets.TryGetValue(jobId, out var set))
        {
            set = new BiSData { JobId = jobId, SourceType = "manual", Name = "Manual Set" };
            charData.BisSets[jobId] = set;
        }

        set.FoodId = foodItemId;
        plugin.Configuration.Save();
    }

    // ── Etro.gg API ────────────────────────────────────────────────

    private readonly Dictionary<string, EtroGearsetDetail> cachedGearsetDetails = new();

    public EtroGearsetDetail? GetCachedGearsetDetail(string id)
    {
        return cachedGearsetDetails.TryGetValue(id, out var detail) ? detail : null;
    }

    public void FetchEtroGearsetDetail(string id)
    {
        if (cachedGearsetDetails.ContainsKey(id)) return;
        Task.Run(async () =>
        {
            try
            {
                var response = await plugin.HttpClient.GetStringAsync($"https://etro.gg/api/gearsets/{id}");
                var data = System.Text.Json.JsonSerializer.Deserialize<EtroResponse>(response);
                if (data == null) return;

                var detail = new EtroGearsetDetail();
                var itemSheet = dataManager.GetExcelSheet<Item>();

                void AddDetailSlot(int slotId, uint? itemId)
                {
                    if (itemId == null || itemId == 0 || itemSheet == null) return;
                    if (itemSheet.TryGetRow(itemId.Value, out var row))
                    {
                        detail.Items[slotId] = new EtroGearsetSlot
                        {
                            ItemId = itemId.Value,
                            Name = row.Name.ToString(),
                            ItemLevel = row.LevelItem.RowId
                        };
                    }
                }

                AddDetailSlot(0, data.weapon);
                AddDetailSlot(1, data.offHand);
                AddDetailSlot(2, data.head);
                AddDetailSlot(3, data.body);
                AddDetailSlot(4, data.hands);
                AddDetailSlot(6, data.legs);
                AddDetailSlot(7, data.feet);
                AddDetailSlot(8, data.ears);
                AddDetailSlot(9, data.neck);
                AddDetailSlot(10, data.wrists);
                AddDetailSlot(11, data.fingerR);
                AddDetailSlot(12, data.fingerL);

                if (data.food.HasValue && data.food.Value > 0)
                {
                    if (cachedEtroFoodMap == null)
                    {
                        var foodJson = await plugin.HttpClient.GetStringAsync("https://etro.gg/api/food/");
                        var foodList = System.Text.Json.JsonSerializer.Deserialize<List<EtroFoodItem>>(foodJson);
                        if (foodList != null)
                            cachedEtroFoodMap = foodList.ToDictionary(f => f.id, f => f.item);
                    }
                    if (cachedEtroFoodMap != null && cachedEtroFoodMap.TryGetValue(data.food.Value, out var mappedItem))
                        detail.FoodId = mappedItem;
                }

                cachedGearsetDetails[id] = detail;
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to fetch Etro gearset detail for {id}");
            }
        });
    }

    private List<EtroBiSSet>? cachedEtroSets = null;
    private DateTime cachedEtroSetsTime = DateTime.MinValue;
    private static readonly TimeSpan EtroCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<List<EtroBiSSet>> GetEtroSets(uint jobId)
    {
        if (cachedEtroSets == null || DateTime.UtcNow - cachedEtroSetsTime > EtroCacheDuration)
        {
            try
            {
                var json = await plugin.HttpClient.GetStringAsync("https://etro.gg/api/gearsets/bis/");
                cachedEtroSets = System.Text.Json.JsonSerializer.Deserialize<List<EtroBiSSet>>(json);
                cachedEtroSetsTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to fetch Etro BiS list");
                return new List<EtroBiSSet>();
            }
        }

        if (cachedEtroSets == null) return new List<EtroBiSSet>();

        // Extract GCD from totalParams for each set
        foreach (var set in cachedEtroSets)
        {
            if (set.totalParams != null)
            {
                foreach (var param in set.totalParams)
                {
                    if (param.TryGetProperty("name", out var nameElem) &&
                        param.TryGetProperty("value", out var valElem) &&
                        valElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        var name = nameElem.GetString();
                        if (name == "GCD" && set.gcd == 0)
                        {
                            set.gcd = valElem.GetSingle();
                        }
                        else if (name == "Average Item Level")
                        {
                            set.AverageItemLevel = (int)valElem.GetSingle();
                        }
                    }
                }
            }
        }

        return cachedEtroSets.Where(s => s.job == (int)jobId).ToList();
    }
}
