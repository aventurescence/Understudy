using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Understudy.Managers;

/// <summary>
/// Scans SpecialShop and related Lumina sheets at startup to discover the current
/// raid tier's gear prefixes, item keywords, book/material item IDs, and raid names.
/// Replaces all hardcoded TierConfig constants with data-driven values.
/// </summary>
public class TierDiscovery
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    // ── Discovered Gear Prefixes ──────────────────────────────────
    public string SavageGearPrefix { get; private set; } = string.Empty;
    public string AugmentedTomeGearPrefix { get; private set; } = string.Empty;
    public string BaseTomeGearPrefix { get; private set; } = string.Empty;

    // ── Discovered Item Keywords ──────────────────────────────────
    public string BookKeyword { get; private set; } = string.Empty;
    public string MaterialKeyword { get; private set; } = string.Empty;
    public string CofferKeyword => SavageGearPrefix;
    public string UniversalTomestoneKeyword { get; private set; } = string.Empty;

    // ── Discovered Display ────────────────────────────────────────
    public string CofferDisplayTrim => string.IsNullOrEmpty(SavageGearPrefix) ? string.Empty : SavageGearPrefix + "'s ";

    // ── Discovered Raid Names ─────────────────────────────────────
    public string[] RaidNames { get; private set; } = Array.Empty<string>();

    // ── Discovered Item Level ─────────────────────────────────────
    public uint TomeGearItemLevel { get; private set; }
    public uint FoodMinItemLevel { get; private set; }

    // ── Discovered Item IDs (for MiscellanyManager) ──────────────
    public Dictionary<string, uint> BookItemIds { get; } = new(); // "M1"->itemId
    public Dictionary<string, ushort> BookIconIds { get; } = new();
    public uint TwineItemId { get; private set; }
    public ushort TwineIconId { get; private set; }
    public uint GlazeItemId { get; private set; }
    public ushort GlazeIconId { get; private set; }
    public uint SolventItemId { get; private set; }
    public ushort SolventIconId { get; private set; }
    public uint UniversalTomestoneItemId { get; private set; }
    public ushort UniversalTomestoneIconId { get; private set; }

    public TierDiscovery(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
        Discover();
    }

    private void Discover()
    {
        var shopSheet = dataManager.GetExcelSheet<SpecialShop>();
        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (shopSheet == null || itemSheet == null) return;

        // ── Phase 1: Find current-tier tomestone gear ────────────

        var tomeEntries = new List<(uint ReceivedItemId, string ReceivedName, uint ItemLevel, uint CostItemId)>();

        foreach (var shop in shopSheet)
        {
            foreach (var entry in shop.Item)
            {
                foreach (var cost in entry.ItemCosts)
                {
                    if (cost.CurrencyCost == 0 || !cost.ItemCost.IsValid || cost.ItemCost.RowId == 0) continue;
                    var costName = cost.ItemCost.Value.Name.ToString();
                    if (!costName.Contains("Tomestone")) continue;

                    foreach (var recv in entry.ReceiveItems)
                    {
                        if (!recv.Item.IsValid || recv.Item.RowId == 0) continue;
                        var recvItem = recv.Item.Value;
                        var recvName = recvItem.Name.ToString();
                        if (string.IsNullOrEmpty(recvName)) continue;

                        tomeEntries.Add((recvItem.RowId, recvName, recvItem.LevelItem.RowId, cost.ItemCost.RowId));
                    }
                }
            }
        }

        if (tomeEntries.Count == 0)
        {
            log.Warning("[TierDiscovery] No tomestone shop entries found");
            return;
        }

        var maxIL = tomeEntries.Max(e => e.ItemLevel);
        var currentTomeEntries = tomeEntries.Where(e => e.ItemLevel == maxIL).ToList();
        TomeGearItemLevel = maxIL;
        FoodMinItemLevel = maxIL > 90 ? maxIL - 90 : 1;

        var tomeGearIds = new HashSet<uint>(currentTomeEntries.Select(e => e.ReceivedItemId));
        var tomeGearNames = currentTomeEntries.Select(e => e.ReceivedName).Distinct().ToList();
        BaseTomeGearPrefix = FindCommonPrefix(tomeGearNames);

        log.Information("[TierDiscovery] Tome gear: iLvl={IL}, prefix=\"{Prefix}\", {Count} items",
            maxIL, BaseTomeGearPrefix, tomeGearIds.Count);

        // ── Phase 2: Find universal tomestone from weapon entries ──

        foreach (var entry in currentTomeEntries)
        {
            if (!itemSheet.TryGetRow(entry.ReceivedItemId, out var recvItem)) continue;
            if (!recvItem.EquipSlotCategory.IsValid) continue;
            if (recvItem.EquipSlotCategory.Value.MainHand == 0) continue;

            foreach (var shop in shopSheet)
            {
                foreach (var shopEntry in shop.Item)
                {
                    bool hasThisItem = false;
                    foreach (var recv in shopEntry.ReceiveItems)
                    {
                        if (recv.Item.IsValid && recv.Item.RowId == entry.ReceivedItemId)
                        { hasThisItem = true; break; }
                    }
                    if (!hasThisItem) continue;

                    foreach (var cost in shopEntry.ItemCosts)
                    {
                        if (!cost.ItemCost.IsValid || cost.ItemCost.RowId == 0 || cost.CurrencyCost == 0) continue;
                        var costName = cost.ItemCost.Value.Name.ToString();

                        if (costName.Contains("Universal Tomestone", StringComparison.OrdinalIgnoreCase))
                        {
                            UniversalTomestoneItemId = cost.ItemCost.RowId;
                            UniversalTomestoneKeyword = costName;
                            UniversalTomestoneIconId = cost.ItemCost.Value.Icon;
                            log.Information("[TierDiscovery] Universal Tomestone: \"{Name}\" (ID {Id})", costName, cost.ItemCost.RowId);
                            break;
                        }
                    }
                }
            }
            if (UniversalTomestoneItemId != 0) break;
        }

        // ── Phase 3: Find augmentation shops → augmented prefix + materials ──

        var augNames = new List<string>();
        var materialIds = new HashSet<uint>();

        foreach (var shop in shopSheet)
        {
            foreach (var entry in shop.Item)
            {
                bool hasTomeGearCost = false;
                var otherCostItems = new List<(uint Id, string Name, ushort Icon)>();

                foreach (var cost in entry.ItemCosts)
                {
                    if (!cost.ItemCost.IsValid || cost.ItemCost.RowId == 0 || cost.CurrencyCost == 0) continue;
                    if (tomeGearIds.Contains(cost.ItemCost.RowId))
                        hasTomeGearCost = true;
                    else
                        otherCostItems.Add((cost.ItemCost.RowId, cost.ItemCost.Value.Name.ToString(), cost.ItemCost.Value.Icon));
                }

                if (!hasTomeGearCost || otherCostItems.Count == 0) continue;

                foreach (var recv in entry.ReceiveItems)
                {
                    if (!recv.Item.IsValid || recv.Item.RowId == 0) continue;
                    var name = recv.Item.Value.Name.ToString();
                    if (!string.IsNullOrEmpty(name))
                        augNames.Add(name);
                }

                foreach (var mat in otherCostItems)
                {
                    if (materialIds.Add(mat.Id))
                    {
                        ClassifyMaterial(mat.Name, mat.Id, mat.Icon);
                    }
                }
            }
        }

        if (augNames.Count > 0)
        {
            AugmentedTomeGearPrefix = FindCommonPrefix(augNames.Distinct().ToList());
            log.Information("[TierDiscovery] Augmented prefix: \"{Prefix}\"", AugmentedTomeGearPrefix);
        }

        if (materialIds.Count > 0)
        {
            var matNames = new List<string>();
            foreach (var id in materialIds)
            {
                if (itemSheet.TryGetRow(id, out var matItem))
                    matNames.Add(matItem.Name.ToString());
            }
            MaterialKeyword = FindCommonPrefix(matNames);
            log.Information("[TierDiscovery] Material keyword: \"{Keyword}\"", MaterialKeyword);
        }

        if (TwineItemId == 0 || GlazeItemId == 0 || SolventItemId == 0)
        {
            FallbackDiscoverMaterials(itemSheet);
        }

        // ── Phase 4: Find book exchange shops → savage prefix + books ──

        var bookCostItems = new Dictionary<uint, string>(); // itemId -> name
        var savageNames = new List<string>();
        uint savageMaxIL = 0;

        foreach (var shop in shopSheet)
        {
            foreach (var entry in shop.Item)
            {
                var bookCosts = new List<(uint Id, string Name, ushort Icon)>();
                bool hasTomeCost = false;

                foreach (var cost in entry.ItemCosts)
                {
                    if (!cost.ItemCost.IsValid || cost.ItemCost.RowId == 0 || cost.CurrencyCost == 0) continue;
                    var costName = cost.ItemCost.Value.Name.ToString();
                    if (costName.Contains("Tomestone")) { hasTomeCost = true; continue; }
                    if (tomeGearIds.Contains(cost.ItemCost.RowId)) continue;

                    if (EndsWithRomanNumeral(costName))
                        bookCosts.Add((cost.ItemCost.RowId, costName, cost.ItemCost.Value.Icon));
                }

                if (hasTomeCost || bookCosts.Count == 0) continue;

                foreach (var recv in entry.ReceiveItems)
                {
                    if (!recv.Item.IsValid || recv.Item.RowId == 0) continue;
                    var recvItem = recv.Item.Value;
                    var il = recvItem.LevelItem.RowId;
                    var name = recvItem.Name.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    if (il > savageMaxIL) savageMaxIL = il;
                    if (il >= savageMaxIL)
                        savageNames.Add(name);
                }

                foreach (var book in bookCosts)
                    bookCostItems.TryAdd(book.Id, book.Name);
            }
        }

        if (savageMaxIL > 0)
        {
            var filteredSavageNames = new List<string>();
            foreach (var shop in shopSheet)
            {
                foreach (var entry in shop.Item)
                {
                    bool hasBook = false;
                    foreach (var cost in entry.ItemCosts)
                    {
                        if (cost.ItemCost.IsValid && bookCostItems.ContainsKey(cost.ItemCost.RowId))
                        { hasBook = true; break; }
                    }
                    if (!hasBook) continue;

                    foreach (var recv in entry.ReceiveItems)
                    {
                        if (!recv.Item.IsValid || recv.Item.RowId == 0) continue;
                        if (recv.Item.Value.LevelItem.RowId == savageMaxIL)
                            filteredSavageNames.Add(recv.Item.Value.Name.ToString());
                    }
                }
            }

            if (filteredSavageNames.Count > 0)
                SavageGearPrefix = FindCommonPrefix(filteredSavageNames.Distinct().ToList());
        }

        log.Information("[TierDiscovery] Savage prefix: \"{Prefix}\", iLvl={IL}", SavageGearPrefix, savageMaxIL);

        if (savageMaxIL > 0)
        {
            var currentTierBookIds = new HashSet<uint>();
            foreach (var shop in shopSheet)
            {
                foreach (var entry in shop.Item)
                {
                    bool yieldsCurrentTier = false;
                    foreach (var recv in entry.ReceiveItems)
                    {
                        if (!recv.Item.IsValid || recv.Item.RowId == 0) continue;
                        var il = recv.Item.Value.LevelItem.RowId;
                        if (il >= savageMaxIL - 10)
                        { yieldsCurrentTier = true; break; }
                    }
                    if (!yieldsCurrentTier) continue;

                    foreach (var cost in entry.ItemCosts)
                    {
                        if (cost.ItemCost.IsValid && bookCostItems.ContainsKey(cost.ItemCost.RowId))
                            currentTierBookIds.Add(cost.ItemCost.RowId);
                    }
                }
            }

            var staleIds = bookCostItems.Keys.Where(id => !currentTierBookIds.Contains(id)).ToList();
            foreach (var id in staleIds)
                bookCostItems.Remove(id);
        }

        if (bookCostItems.Count > 0)
        {
            var bookNames = bookCostItems.Values.ToList();
            BookKeyword = FindCommonPrefix(bookNames);
            log.Information("[TierDiscovery] Book keyword: \"{Keyword}\", {Count} books", BookKeyword, bookCostItems.Count);

            foreach (var (id, name) in bookCostItems)
            {
                int floor = DetermineFloor(name);
                if (floor > 0)
                {
                    var key = $"M{floor}";
                    BookItemIds[key] = id;
                    if (itemSheet.TryGetRow(id, out var bookItem))
                        BookIconIds[key] = bookItem.Icon;
                }
            }
        }

        // ── Phase 5: Discover raid names from ContentFinderCondition ──

        DiscoverRaidNames();

        // ── Summary ──
        log.Information("[TierDiscovery] Complete: Tome=\"{Tome}\" Aug=\"{Aug}\" Savage=\"{Savage}\" Book=\"{Book}\" Mat=\"{Mat}\" Raids={Raids}",
            BaseTomeGearPrefix, AugmentedTomeGearPrefix, SavageGearPrefix, BookKeyword, MaterialKeyword, RaidNames.Length);
    }

    // ── Raid Discovery ────────────────────────────────────────────

    private void DiscoverRaidNames()
    {
        var cfcSheet = dataManager.GetExcelSheet<ContentFinderCondition>();
        if (cfcSheet == null) return;

        var savageRaids = new List<(string Name, uint SortKey)>();
        foreach (var row in cfcSheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.Contains("(Savage)")) continue;

            if (!string.IsNullOrEmpty(SavageGearPrefix) && name.Contains(SavageGearPrefix, StringComparison.OrdinalIgnoreCase))
            {
                savageRaids.Add((name, row.SortKey));
            }
        }

        if (savageRaids.Count == 0)
        {
            var allSavage = new List<(string Name, uint SortKey)>();
            foreach (var row in cfcSheet)
            {
                var name = row.Name.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                if (!name.Contains("(Savage)")) continue;
                allSavage.Add((name, row.SortKey));
            }
            if (allSavage.Count == 0) return;

            savageRaids = allSavage.OrderByDescending(r => r.SortKey).Take(4).ToList();
        }

        var currentTier = savageRaids
            .OrderBy(r => r.SortKey)
            .Select(r => r.Name)
            .Take(4)
            .ToArray();

        RaidNames = currentTier;
        log.Information("[TierDiscovery] Raids: {Raids}", string.Join(", ", RaidNames));
    }

    // ── Material Classification ───────────────────────────────────

    private void ClassifyMaterial(string name, uint itemId, ushort iconId)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("twine"))
        {
            TwineItemId = itemId; TwineIconId = iconId;
            log.Information("[TierDiscovery] Twine: \"{Name}\" (ID {Id})", name, itemId);
        }
        else if (lower.Contains("glaze"))
        {
            GlazeItemId = itemId; GlazeIconId = iconId;
            log.Information("[TierDiscovery] Glaze: \"{Name}\" (ID {Id})", name, itemId);
        }
        else if (lower.Contains("solvent") || lower.Contains("ester"))
        {
            SolventItemId = itemId; SolventIconId = iconId;
            log.Information("[TierDiscovery] Solvent/Ester: \"{Name}\" (ID {Id})", name, itemId);
        }
    }

    /// <summary>
    /// Fallback: scans the Item sheet for missing upgrade materials (Twine, Glaze, Solvent/Ester).
    /// Uses two strategies:
    /// 1. Prefix match from known material (e.g., "Thundersteeped" from "Thundersteeped Solvent")
    /// 2. RowId proximity — upgrade materials for the same tier have adjacent RowIds
    /// </summary>
    private void FallbackDiscoverMaterials(Lumina.Excel.ExcelSheet<Item> itemSheet)
    {
        var materialSuffixes = new[] { " Twine", " Glaze", " Solvent", " Ester" };

        uint knownId = SolventItemId != 0 ? SolventItemId : TwineItemId != 0 ? TwineItemId : GlazeItemId;
        string? prefix = null;

        if (knownId != 0 && itemSheet.TryGetRow(knownId, out var knownItem))
        {
            var knownName = knownItem.Name.ToString();
            foreach (var suffix in materialSuffixes)
            {
                if (knownName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = knownName[..^suffix.Length];
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            log.Information("[TierDiscovery] Fallback: scanning for materials with prefix \"{Prefix}\"", prefix);
            foreach (var item in itemSheet)
            {
                var name = item.Name.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    ClassifyMaterial(name, item.RowId, item.Icon);
            }
        }

        // Check nearby RowIds — tier materials have adjacent IDs
        if (knownId != 0 && (TwineItemId == 0 || GlazeItemId == 0 || SolventItemId == 0))
        {
            log.Information("[TierDiscovery] Fallback: checking RowIds near {Id} for remaining materials", knownId);
            for (uint offset = 1; offset <= 10; offset++)
            {
                foreach (var candidateId in new[] { knownId + offset, knownId > offset ? knownId - offset : 0 })
                {
                    if (candidateId == 0) continue;
                    if (!itemSheet.TryGetRow(candidateId, out var candidate)) continue;
                    var name = candidate.Name.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    foreach (var suffix in materialSuffixes)
                    {
                        if (name.EndsWith(suffix.TrimStart(), StringComparison.OrdinalIgnoreCase))
                        {
                            ClassifyMaterial(name, candidateId, candidate.Icon);
                            break;
                        }
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(prefix))
            MaterialKeyword = prefix;
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static string FindCommonPrefix(List<string> names)
    {
        if (names.Count == 0) return string.Empty;
        if (names.Count == 1) return names[0];

        var prefix = names[0];
        foreach (var name in names.Skip(1))
        {
            while (!name.StartsWith(prefix, StringComparison.Ordinal) && prefix.Length > 0)
                prefix = prefix[..^1];
        }

        return prefix.TrimEnd();
    }

    private static bool EndsWithRomanNumeral(string name)
    {
        name = name.Trim();
        return name.EndsWith(" IV", StringComparison.Ordinal)
            || name.EndsWith(" III", StringComparison.Ordinal)
            || name.EndsWith(" II", StringComparison.Ordinal)
            || name.EndsWith(" I", StringComparison.Ordinal);
    }

    private static int DetermineFloor(string name)
    {
        name = name.Trim();
        if (name.EndsWith(" IV", StringComparison.Ordinal)) return 4;
        if (name.EndsWith(" III", StringComparison.Ordinal)) return 3;
        if (name.EndsWith(" II", StringComparison.Ordinal)) return 2;
        if (name.EndsWith(" I", StringComparison.Ordinal)) return 1;
        return 0;
    }
}
