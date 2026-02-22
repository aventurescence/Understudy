using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using Understudy.Models;

namespace Understudy.Managers;

public class BiSComparisonManager
{
    private Dictionary<uint, int>? cachedTomeCosts = null;
    private Dictionary<uint, int>? cachedBookCosts = null;
    private readonly Plugin plugin;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    public BiSComparisonManager(Plugin plugin, IDataManager dataManager, IPluginLog log)
    {
        this.plugin = plugin;
        this.dataManager = dataManager;
        this.log = log;
    }

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
}
