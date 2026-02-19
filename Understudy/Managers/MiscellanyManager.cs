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
/// Searches in: player inventory, chocobo saddlebag, and premium saddlebag.
/// </summary>
public unsafe class MiscellanyManager
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    // Item IDs searched by Lumina on startup
    private readonly Dictionary<string, uint> bookItemIds = new();   // "M1"->itemId
    private uint twineItemId;
    private uint glazeItemId;
    private uint solventItemId;
    private uint universalTomestoneItemId;
    private readonly List<(uint id, string name)> cofferItems = new();

    // Icon IDs for each item to render in the UI
    private readonly Dictionary<string, ushort> bookIconIds = new();  // "M1"->iconId
    private ushort twineIconId;
    private ushort glazeIconId;
    private ushort solventIconId;
    private ushort universalTomestoneIconId;
    private readonly Dictionary<string, ushort> cofferIconIds = new(); // nombre->iconId

    // Fast set for lookup during scanning
    private readonly HashSet<uint> trackedIds = new();

    // Keywords by category — sourced from TierConfig
    private const string BookKeyword = TierConfig.BookKeyword;
    private const string MaterialKeyword = TierConfig.MaterialKeyword;
    private const string CofferKeyword = TierConfig.CofferKeyword;
    private const string UniversalTomestoneKeyword = TierConfig.UniversalTomestoneKeyword;

    public MiscellanyManager(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
        LookupItemIds();
    }

    /// <summary>
    /// Searches Lumina for item IDs of the current tier using keywords by category:
    ///   - Books: "AAC Illustrated" (HW Edition I-IV)
    ///   - Materials: "Thundersteeping" (Twine/Glaze/Solvent)
    ///   - Coffers: "Grand Champion" (equipment coffers)
    /// </summary>
    private void LookupItemIds()
    {
        var sheet = dataManager.GetExcelSheet<Item>();
        if (sheet == null) return;

        foreach (var item in sheet)
        {
            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            // ── Raid Books ──
            if (name.Contains(BookKeyword, StringComparison.OrdinalIgnoreCase))
            {
                int floor = DetermineFloor(name);
                if (floor > 0)
                {
                    var key = $"M{floor}";
                    bookItemIds[key] = item.RowId;
                    bookIconIds[key] = item.Icon;
                    trackedIds.Add(item.RowId);
                    log.Debug("Miscellany book: {Name} (ID {Id}, Icon {Icon}) -> {Key}", name, item.RowId, item.Icon, key);
                }
            }

            // ── Upgrade Materials (Twine/Glaze/Solvent) ──
            if (name.Contains(MaterialKeyword, StringComparison.OrdinalIgnoreCase))
            {
                var lower = name.ToLowerInvariant();
                if (lower.Contains("twine"))
                {
                    twineItemId = item.RowId;
                    twineIconId = item.Icon;
                    trackedIds.Add(item.RowId);
                    log.Debug("Miscellany twine: {Name} (ID {Id}, Icon {Icon})", name, item.RowId, item.Icon);
                }
                else if (lower.Contains("glaze"))
                {
                    glazeItemId = item.RowId;
                    glazeIconId = item.Icon;
                    trackedIds.Add(item.RowId);
                    log.Debug("Miscellany glaze: {Name} (ID {Id}, Icon {Icon})", name, item.RowId, item.Icon);
                }
                else if (lower.Contains("solvent"))
                {
                    solventItemId = item.RowId;
                    solventIconId = item.Icon;
                    trackedIds.Add(item.RowId);
                    log.Debug("Miscellany solvent: {Name} (ID {Id}, Icon {Icon})", name, item.RowId, item.Icon);
                }
                else
                {
                    // Log items that match MaterialKeyword but not subcategories
                    log.Information("Miscellany UNMATCHED material: {Name} (ID {Id}, Icon {Icon})", name, item.RowId, item.Icon);
                }
            }

            // ── Unopened Coffers ──
            if (name.Contains(CofferKeyword, StringComparison.OrdinalIgnoreCase)
                && name.Contains("Coffer", StringComparison.OrdinalIgnoreCase))
            {
                cofferItems.Add((item.RowId, name));
                cofferIconIds[name] = item.Icon;
                trackedIds.Add(item.RowId);
                log.Debug("Miscellany coffer: {Name} (ID {Id}, Icon {Icon})", name, item.RowId, item.Icon);
            }

            // ── Universal Tomestone 3.0 ──
            if (name.Contains(UniversalTomestoneKeyword, StringComparison.OrdinalIgnoreCase)
                && universalTomestoneItemId == 0)
            {
                universalTomestoneItemId = item.RowId;
                universalTomestoneIconId = item.Icon;
                trackedIds.Add(item.RowId);
                log.Debug("Miscellany Universal Tomestone: {Name} (ID {Id}, Icon {Icon})", name, item.RowId, item.Icon);
            }
        }

        log.Information("Miscellany init: {BookCount} books, twine={Twine}, glaze={Glaze}, solvent={Solvent}, {CofferCount} coffers",
            bookItemIds.Count, twineItemId, glazeItemId, solventItemId, cofferItems.Count);
    }

    /// <summary>
    /// Determines the raid floor based on the roman numeral at the end of the name.
    /// </summary>
    private static int DetermineFloor(string name)
    {
        name = name.Trim();
        if (name.EndsWith(" IV", StringComparison.Ordinal)) return 4;
        if (name.EndsWith(" III", StringComparison.Ordinal)) return 3;
        if (name.EndsWith(" II", StringComparison.Ordinal)) return 2;
        if (name.EndsWith(" I", StringComparison.Ordinal)) return 1;
        return 0;
    }

    /// <summary>
    /// Gets the icon ID of a miscellany item by category and key.
    /// </summary>
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

        // Initialize book counts to 0
        foreach (var key in bookItemIds.Keys)
            data.BookCounts[key] = 0;

        // Ensure M1-M4 exist even if no items were found
        for (int i = 1; i <= 4; i++)
        {
            var key = $"M{i}";
            if (!data.BookCounts.ContainsKey(key))
                data.BookCounts[key] = 0;
        }

        if (trackedIds.Count == 0) return data;

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return data;

        // Containers to scan: inventory + saddlebag + premium saddlebag
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

        // Accumulate quantities by itemId
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

        // Map counts to data structure
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
