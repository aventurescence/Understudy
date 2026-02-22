using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Understudy.Models;

namespace Understudy.Managers;

public class EtroBrowseManager
{
    private readonly Plugin plugin;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    private readonly Dictionary<string, EtroGearsetDetail> cachedGearsetDetails = new();

    private List<EtroBiSSet>? cachedEtroSets = null;
    private DateTime cachedEtroSetsTime = DateTime.MinValue;
    private static readonly TimeSpan EtroCacheDuration = TimeSpan.FromMinutes(5);

    // EtroResponse DTO shared with import — duplicated here for self-containment
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

    private class EtroFoodItem { public uint id { get; set; } public uint item { get; set; } }
    private Dictionary<uint, uint>? cachedEtroFoodMap = null;

    public EtroBrowseManager(Plugin plugin, IDataManager dataManager, IPluginLog log)
    {
        this.plugin = plugin;
        this.dataManager = dataManager;
        this.log = log;
    }

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
}
