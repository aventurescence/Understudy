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
    private Dictionary<uint, int>? cachedTomeCosts = null;
    private Dictionary<uint, int>? cachedBookCosts = null;
    private readonly Plugin plugin;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    
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

    public void ImportFromUrl(string url, uint jobId, ulong? characterId = null)
    {
        var targetId = characterId ?? Plugin.PlayerState.ContentId;
        Task.Run(async () => await ImportAsync(url, jobId, targetId));
    }

    private async Task ImportAsync(string url, uint jobId, ulong targetCharacterId)
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
                await ImportEtro(etroMatch.Groups[1].Value, jobId, url, targetCharacterId);
                plugin.CheckAndTrackGear(jobId, targetCharacterId);
                return;
            }

            var xivMatch = XivGearPattern.Match(url);
            if (xivMatch.Success)
            {
                await ImportXivGear(xivMatch.Groups[1].Value, xivMatch.Groups[2].Value, jobId, url, targetCharacterId);
                plugin.CheckAndTrackGear(jobId, targetCharacterId);
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

    private async Task ImportEtro(string id, uint jobId, string originalUrl, ulong targetCharacterId)
    {
        var response = await plugin.HttpClient.GetStringAsync($"https://etro.gg/api/gearsets/{id}");
        var data = System.Text.Json.JsonSerializer.Deserialize<EtroResponse>(response);
        if (data == null) throw new Exception("Empty response from Etro");

        uint resolvedFoodId = await ResolveEtroFoodIdAsync(data.food);

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

        SaveBiS(jobId, bis, targetCharacterId);
    }

    // ── XIVGear.app Import ─────────────────────────────────────────

    private class XGSetCollection { public string? name { get; set; } public List<XGSet>? sets { get; set; } }
    private class XGSet { public string? name { get; set; } public Dictionary<string, XGItem>? items { get; set; } }
    private class XGItem { public uint id { get; set; } public List<XGMateria>? materia { get; set; } }
    private class XGMateria { public int id { get; set; } }

    private async Task ImportXivGear(string shortcode, string setIndexStr, uint jobId, string originalUrl, ulong targetCharacterId)
    {
        var json = await plugin.HttpClient.GetStringAsync($"https://api.xivgear.app/shortlink/{shortcode}");
        
        XGSet? targetSet = null;

        var coll = System.Text.Json.JsonSerializer.Deserialize<XGSetCollection>(json);
        if (coll?.sets != null && coll.sets.Count > 0)
        {
            int idx = int.TryParse(setIndexStr, out var i) ? i : 0;
            if (idx < coll.sets.Count)
                targetSet = coll.sets[idx];
            else
                throw new Exception($"Set index {idx} is out of range (collection has {coll.sets.Count} sets)");
        }

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

        SaveBiS(jobId, bis, targetCharacterId);
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
                string floor = GetSavageFloor(item.Slot);
                if (costs.BooksNeeded.ContainsKey(floor))
                    costs.BooksNeeded[floor]++;
                else
                    costs.BooksNeeded[floor] = 1;
                return $"{floor} Drop / Book";

            case GearSource.AugmentedTomestone:
                AccumulateTomeCosts(item.ItemId, item.Slot, ref costs);
                AccumulateUpgradeMaterialCosts(item.Slot, ref costs);
                return "Tomes + Upgrade";

            case GearSource.Tomestone:
                int tomes = AccumulateTomeCosts(item.ItemId, item.Slot, ref costs);
                return $"{tomes} Tomes";
        }

        return "Unknown";
    }

    public uint ResolveEtroFoodId(uint etroFoodId)
    {
        if (cachedEtroFoodMap != null && cachedEtroFoodMap.TryGetValue(etroFoodId, out var ffxivId))
            return ffxivId;
        return 0;
    }

    private async Task<uint> ResolveEtroFoodIdAsync(uint? etroFoodId)
    {
        if (!etroFoodId.HasValue || etroFoodId.Value == 0) return 0;
        try
        {
            await EnsureEtroFoodMapLoaded();
            if (cachedEtroFoodMap != null && cachedEtroFoodMap.TryGetValue(etroFoodId.Value, out var mappedItem))
                return mappedItem;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"[Understudy] Failed to fetch Etro food mapping for id {etroFoodId}");
        }
        return 0;
    }

    private async Task EnsureEtroFoodMapLoaded()
    {
        if (cachedEtroFoodMap != null) return;
        var foodJson = await plugin.HttpClient.GetStringAsync("https://etro.gg/api/food/");
        var foodList = System.Text.Json.JsonSerializer.Deserialize<List<EtroFoodItem>>(foodJson);
        if (foodList != null)
            cachedEtroFoodMap = foodList.ToDictionary(f => f.id, f => f.item);
    }

    private void SaveBiS(uint jobId, BiSData bis, ulong characterId)
    {
        if (characterId == 0) return;

        if (plugin.IsJobExcluded(jobId)) return;

        if (plugin.Configuration.Characters.TryGetValue(characterId, out var charData))
        {
            charData.BisSets[jobId] = bis;
            plugin.Configuration.Save();
        }
    }

    // ── Comparison Logic ───────────────────────────────────────────

    public (List<BiSSlotComparison> Comparisons, AcquisitionCosts Costs) Compare(uint jobId, ulong? characterId = null)
    {
        var costs = new AcquisitionCosts();
        var comparisons = new List<BiSSlotComparison>();

        var contentId = characterId ?? Plugin.PlayerState.ContentId;
        if (contentId == 0 || !plugin.Configuration.Characters.TryGetValue(contentId, out var charData))
            return (comparisons, costs);

        if (!charData.BisSets.TryGetValue(jobId, out var bisData))
            return (comparisons, costs);

        var currentGear = charData.GearSets.TryGetValue(jobId, out var set) ? set : new GearSetData();

        var equippedRings = currentGear.Items.Where(x => x.Slot == 11 || x.Slot == 12).ToList();

        // Greedily match BiS rings against equipped ring pool
        var ringMatches = new Dictionary<int, GearItem?>();
        var unmatchedBisSlots = new List<int>();
        foreach (var kvp in bisData.Items.Where(k => k.Key == 11 || k.Key == 12).OrderBy(k => k.Key))
        {
            var match = equippedRings.FirstOrDefault(r => r.ItemId == kvp.Value.ItemId);
            if (match != null)
            {
                ringMatches[kvp.Key] = match;
                equippedRings.Remove(match);
            }
            else
            {
                unmatchedBisSlots.Add(kvp.Key);
            }
        }
        foreach (var bisSlot in unmatchedBisSlots)
        {
            if (equippedRings.Count > 0)
            {
                ringMatches[bisSlot] = equippedRings[0];
                equippedRings.RemoveAt(0);
            }
            else
            {
                ringMatches[bisSlot] = null;
            }
        }

        foreach (var kvp in bisData.Items.OrderBy(k => k.Key))
        {
            var slot = kvp.Key;
            var bisItem = kvp.Value;
            GearItem? currentItem;
            bool isOwned;

            if (slot == 11 || slot == 12)
            {
                ringMatches.TryGetValue(slot, out currentItem);
                isOwned = currentItem != null && currentItem.ItemId == bisItem.ItemId;
            }
            else
            {
                currentItem = currentGear.Items.FirstOrDefault(x => x.Slot == slot);
                isOwned = currentItem != null && currentItem.ItemId == bisItem.ItemId;
            }

            var label = "";

            if (!isOwned)
            {
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
        switch (item.Source)
        {
            case GearSource.Savage:
                if (!string.IsNullOrEmpty(item.FloorSource))
                {
                    int bookCost = GetBookCost(item.ItemId);
                    costs.BooksNeeded[item.FloorSource] += bookCost;
                    return $"{item.FloorSource} Savage";
                }
                break;

            case GearSource.AugmentedTomestone:
                AccumulateTomeCosts(item.ItemId, item.Slot, ref costs);
                AccumulateUpgradeMaterialCosts(item.Slot, ref costs);
                return "Tomes + Upgrade";

            case GearSource.Tomestone:
                int tomes = AccumulateTomeCosts(item.ItemId, item.Slot, ref costs);
                return $"{tomes} Tomes";
        }

        return "Unknown";
    }

    private void EnsureShopCostsLoaded()
    {
        if (cachedTomeCosts != null) return;

        cachedTomeCosts = new Dictionary<uint, int>();
        cachedBookCosts = new Dictionary<uint, int>();

        var shopSheet = dataManager.GetExcelSheet<SpecialShop>();
        if (shopSheet == null) return;

        foreach (var shop in shopSheet)
        {
            foreach (var entry in shop.Item)
            {
                foreach (var recv in entry.ReceiveItems)
                {
                    if (!recv.Item.IsValid || recv.Item.RowId == 0) continue;
                    var receivedItemId = recv.Item.RowId;

                    foreach (var cost in entry.ItemCosts)
                    {
                        if (cost.CurrencyCost == 0) continue;
                        if (!cost.ItemCost.IsValid || cost.ItemCost.RowId == 0) continue;

                        var costItemName = cost.ItemCost.Value.Name.ToString();
                        var amount = (int)cost.CurrencyCost;

                        if (costItemName.Contains("Tomestone"))
                            cachedTomeCosts.TryAdd(receivedItemId, amount);
                        else if (costItemName.Contains(TierConfig.BookKeyword))
                            cachedBookCosts.TryAdd(receivedItemId, amount);
                    }
                }
            }
        }

        log.Debug($"[Understudy] Loaded {cachedTomeCosts.Count} tome costs and {cachedBookCosts.Count} book costs from SpecialShop");
    }

    private int GetTomeCost(uint itemId, int slot = -1)
    {
        EnsureShopCostsLoaded();
        if (cachedTomeCosts!.TryGetValue(itemId, out var cost))
            return cost;

        // Slot-based fallback for augmented items not in tome shop
        if (slot >= 0)
        {
            return slot switch
            {
                0 => 500,       // Weapon
                3 or 6 => 825,  // Body, Legs
                2 or 4 or 7 => 495, // Head, Hands, Feet
                _ => 375        // Accessories
            };
        }

        return 0;
    }

    private int GetBookCost(uint itemId)
    {
        EnsureShopCostsLoaded();
        return cachedBookCosts!.TryGetValue(itemId, out var cost) ? cost : 0;
    }

    private static readonly HashSet<int> LeftSideSlots = new() { 2, 3, 4, 6, 7 };

    private int AccumulateTomeCosts(uint itemId, int slot, ref AcquisitionCosts costs)
    {
        int tomes = GetTomeCost(itemId, slot);
        costs.TomestonesNeeded += tomes;
        if (slot == 0) costs.UniversalTomestonesNeeded += 1;
        return tomes;
    }

    private static void AccumulateUpgradeMaterialCosts(int slot, ref AcquisitionCosts costs)
    {
        if (slot == 0)
            costs.SolventNeeded++;
        else if (LeftSideSlots.Contains(slot))
            costs.TwineNeeded++;
        else
            costs.GlazeNeeded++;
    }
    public void SetBiSItem(uint jobId, int slotId, uint itemId, string name, uint itemLevel, List<uint> materia, ulong? characterId = null)
    {
        var contentId = characterId ?? Plugin.PlayerState.ContentId;
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

    public void SetBiSFood(uint jobId, uint foodItemId, ulong? characterId = null)
    {
        var contentId = characterId ?? Plugin.PlayerState.ContentId;
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

                detail.FoodId = await ResolveEtroFoodIdAsync(data.food);

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
