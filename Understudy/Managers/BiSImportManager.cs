using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Understudy.Models;

namespace Understudy.Managers;

public class BiSImportManager
{
    private class EtroFoodItem { public uint id { get; set; } public uint item { get; set; } }
    private Dictionary<uint, uint>? cachedEtroFoodMap = null;
    private readonly Plugin plugin;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    private static readonly Regex EtroPattern = new(@"https?://etro\.gg/gearset/([^/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex XivGearPattern = new(@"(?:https?://xivgear\.app/\?page=sl\||https?://api\.xivgear\.app/shortlink/)([a-zA-Z0-9-]+)(?:&(?:selectedIndex|onlySetIndex)=(\d+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsLoading { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public BiSImportManager(Plugin plugin, IDataManager dataManager, IPluginLog log)
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

        if (plugin.CharacterTracker.IsJobExcluded(jobId))
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
                plugin.CharacterTracker.CheckAndTrackGear(jobId, targetCharacterId);
                return;
            }

            var xivMatch = XivGearPattern.Match(url);
            if (xivMatch.Success)
            {
                await ImportXivGear(xivMatch.Groups[1].Value, xivMatch.Groups[2].Value, jobId, url, targetCharacterId);
                plugin.CharacterTracker.CheckAndTrackGear(jobId, targetCharacterId);
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

        if (plugin.CharacterTracker.IsJobExcluded(jobId)) return;

        if (plugin.Configuration.Characters.TryGetValue(characterId, out var charData))
        {
            charData.BisSets[jobId] = bis;
            plugin.Configuration.Save();
        }
    }

    public void SetBiSItem(uint jobId, int slotId, uint itemId, string name, uint itemLevel, List<uint> materia, ulong? characterId = null)
    {
        var contentId = characterId ?? Plugin.PlayerState.ContentId;
        if (contentId == 0) return;

        if (plugin.CharacterTracker.IsJobExcluded(jobId)) return;

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
}
