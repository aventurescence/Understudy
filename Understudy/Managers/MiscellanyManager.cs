using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Understudy.Managers;

/// <summary>
/// Scans inventories for raid items of the current tier:
/// books (Mythos), twine, glaze, solvent, and unopened coffers.
/// Uses item IDs discovered by TierDiscovery from SpecialShop data.
/// </summary>
public unsafe class MiscellanyManager
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    private readonly Dictionary<string, uint> bookItemIds = new();
    private uint twineItemId;
    private uint glazeItemId;
    private uint solventItemId;
    private uint universalTomestoneItemId;
    private readonly List<(uint id, string name)> cofferItems = new();

    private readonly Dictionary<string, ushort> bookIconIds = new();
    private ushort twineIconId;
    private ushort glazeIconId;
    private ushort solventIconId;
    private ushort universalTomestoneIconId;
    private readonly Dictionary<string, ushort> cofferIconIds = new();

    private readonly HashSet<uint> trackedIds = new();

    public MiscellanyManager(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
        LoadFromDiscovery();
        LookupCoffers();
    }

    /// <summary>
    /// Loads book, material, and universal tomestone IDs from TierDiscovery.
    /// These were already discovered from SpecialShop data.
    /// </summary>
    private void LoadFromDiscovery()
    {
        var discovery = Plugin.TierDiscovery;
        if (discovery == null) return;

        foreach (var (key, id) in discovery.BookItemIds)
        {
            bookItemIds[key] = id;
            trackedIds.Add(id);
        }
        foreach (var (key, icon) in discovery.BookIconIds)
            bookIconIds[key] = icon;

        twineItemId = discovery.TwineItemId;
        twineIconId = discovery.TwineIconId;
        if (twineItemId != 0) trackedIds.Add(twineItemId);

        glazeItemId = discovery.GlazeItemId;
        glazeIconId = discovery.GlazeIconId;
        if (glazeItemId != 0) trackedIds.Add(glazeItemId);

        solventItemId = discovery.SolventItemId;
        solventIconId = discovery.SolventIconId;
        if (solventItemId != 0) trackedIds.Add(solventItemId);

        universalTomestoneItemId = discovery.UniversalTomestoneItemId;
        universalTomestoneIconId = discovery.UniversalTomestoneIconId;
        if (universalTomestoneItemId != 0) trackedIds.Add(universalTomestoneItemId);

        log.Information("Miscellany init from discovery: {BookCount} books, twine={Twine}, glaze={Glaze}, solvent={Solvent}, univTome={Tome}",
            bookItemIds.Count, twineItemId, glazeItemId, solventItemId, universalTomestoneItemId);
    }

    /// <summary>
    /// Searches Lumina for coffers matching the savage gear prefix.
    /// Coffers share the same name prefix as savage gear.
    /// </summary>
    private void LookupCoffers()
    {
        var cofferKeyword = TierConfig.CofferKeyword;
        if (string.IsNullOrEmpty(cofferKeyword)) return;

        var sheet = dataManager.GetExcelSheet<Item>();
        if (sheet == null) return;

        foreach (var item in sheet)
        {
            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            if (name.Contains(cofferKeyword, StringComparison.OrdinalIgnoreCase)
                && name.Contains("Coffer", StringComparison.OrdinalIgnoreCase))
            {
                cofferItems.Add((item.RowId, name));
                cofferIconIds[name] = item.Icon;
                trackedIds.Add(item.RowId);
                log.Debug("Miscellany coffer: {Name} (ID {Id})", name, item.RowId);
            }
        }

        log.Information("Miscellany coffers: {Count} found", cofferItems.Count);
    }

    public ushort GetBookIconId(string key) => bookIconIds.GetValueOrDefault(key);
    public ushort GetTwineIconId() => twineIconId;
    public ushort GetGlazeIconId() => glazeIconId;
    public ushort GetSolventIconId() => solventIconId;
    public ushort GetUniversalTomestoneIconId() => universalTomestoneIconId;
    public ushort GetCofferIconId(string name) => cofferIconIds.GetValueOrDefault(name);

    /// <summary>
    /// Scans accessible inventories and returns counts of items from the current tier.
    /// </summary>
    public MiscellanyData ScanInventories()
    {
        var data = new MiscellanyData { LastUpdated = DateTime.UtcNow };

        foreach (var key in bookItemIds.Keys)
            data.BookCounts[key] = 0;

        for (int i = 1; i <= 4; i++)
        {
            var key = $"M{i}";
            if (!data.BookCounts.ContainsKey(key))
                data.BookCounts[key] = 0;
        }

        if (trackedIds.Count == 0) return data;

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return data;

        var containers = new[]
        {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
            InventoryType.SaddleBag1,
            InventoryType.SaddleBag2,
            InventoryType.PremiumSaddleBag1,
            InventoryType.PremiumSaddleBag2,
        };

        var itemCounts = new Dictionary<uint, int>();

        foreach (var containerType in containers)
        {
            var container = inventoryManager->GetInventoryContainer(containerType);
            if (container == null) continue;

            for (int i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null || slot->ItemId == 0) continue;

                if (trackedIds.Contains(slot->ItemId))
                {
                    itemCounts.TryGetValue(slot->ItemId, out var current);
                    itemCounts[slot->ItemId] = current + (int)slot->Quantity;
                }
            }
        }

        foreach (var kvp in bookItemIds)
            data.BookCounts[kvp.Key] = itemCounts.GetValueOrDefault(kvp.Value);

        data.TwineCount = itemCounts.GetValueOrDefault(twineItemId);
        data.GlazeCount = itemCounts.GetValueOrDefault(glazeItemId);
        data.SolventCount = itemCounts.GetValueOrDefault(solventItemId);
        data.UniversalTomestoneCount = itemCounts.GetValueOrDefault(universalTomestoneItemId);

        foreach (var (id, name) in cofferItems)
        {
            var count = itemCounts.GetValueOrDefault(id);
            if (count > 0)
                data.CofferCounts[name] = count;
        }

        return data;
    }
}
