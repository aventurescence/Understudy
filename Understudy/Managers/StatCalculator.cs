using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Understudy.Models;

namespace Understudy.Managers;

/// <summary>
/// Calculates gear, materia, and food stats from Lumina sheets.
/// Caches materia and BaseParam data on first use.
/// </summary>
public class StatCalculator
{
    private static readonly Dictionary<uint, Func<ItemLevel, int>> ParamToItemLevel = new()
    {
        { 1, il => il.Strength },
        { 2, il => il.Dexterity },
        { 3, il => il.Vitality },
        { 4, il => il.Intelligence },
        { 5, il => il.Mind },
        { 6, il => il.Piety },
        { 19, il => il.Tenacity },
        { 21, il => il.Defense },
        { 22, il => il.DirectHitRate },
        { 24, il => il.MagicDefense },
        { 27, il => il.CriticalHit },
        { 44, il => il.Determination },
        { 45, il => il.SkillSpeed },
        { 46, il => il.SpellSpeed },
    };

    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    private Dictionary<uint, (uint ParamId, int Value)>? materiaLookup;
    private Dictionary<uint, string>? paramNames;
    private List<MateriaOption>? materiaOptions;

    public StatCalculator(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }

    public record struct MateriaOption(uint ItemId, uint ParamId, string StatName, int Value, int Grade);

    // ── Cache Initialization ──────────────────────────────────────

    private void EnsureCachesLoaded()
    {
        if (materiaLookup != null) return;

        materiaLookup = new Dictionary<uint, (uint, int)>();
        paramNames = new Dictionary<uint, string>();
        var optionsList = new List<MateriaOption>();

        var matSheet = dataManager.GetExcelSheet<Materia>();
        if (matSheet != null)
        {
            foreach (var row in matSheet)
            {
                if (!row.BaseParam.IsValid || row.BaseParam.RowId == 0) continue;
                var paramId = row.BaseParam.RowId;

                for (int g = 0; g < row.Item.Count && g < row.Value.Count; g++)
                {
                    var itemRef = row.Item[g];
                    if (!itemRef.IsValid || itemRef.RowId == 0) continue;
                    var statValue = (int)row.Value[g];
                    if (statValue <= 0) continue;

                    materiaLookup.TryAdd(itemRef.RowId, (paramId, statValue));
                    optionsList.Add(new MateriaOption(itemRef.RowId, paramId, "", statValue, g));
                }
            }
        }

        var bpSheet = dataManager.GetExcelSheet<BaseParam>();
        if (bpSheet != null)
        {
            foreach (var row in bpSheet)
            {
                var name = row.Name.ToString();
                if (!string.IsNullOrEmpty(name))
                    paramNames[row.RowId] = name;
            }
        }

        materiaOptions = optionsList
            .Where(o => paramNames!.ContainsKey(o.ParamId))
            .Select(o => o with { StatName = paramNames![o.ParamId] })
            .OrderByDescending(o => o.Grade)
            .ThenBy(o => o.StatName)
            .ToList();

        log.Debug($"[Understudy] StatCalculator: cached {materiaLookup.Count} materia, {paramNames.Count} params");
    }

    // ── Public API ────────────────────────────────────────────────

    public string GetStatName(uint paramId)
    {
        EnsureCachesLoaded();
        return paramNames!.TryGetValue(paramId, out var name) ? name : $"Param#{paramId}";
    }

    public (uint ParamId, int Value) GetMateriaStatBonus(uint materiaItemId)
    {
        EnsureCachesLoaded();
        return materiaLookup!.TryGetValue(materiaItemId, out var result) ? result : (0, 0);
    }

    /// <summary>Returns materia options filtered to combat substats and top N grades.</summary>
    public List<MateriaOption> GetMateriaOptions(int topGrades = 2)
    {
        EnsureCachesLoaded();
        var combatParams = new HashSet<uint> { 27, 44, 22, 19, 6, 45, 46 }; // CRT, DET, DH, TEN, PIE, SKS, SPS
        if (materiaOptions!.Count == 0) return new List<MateriaOption>();
        int maxGrade = materiaOptions.Max(o => o.Grade);
        int minGrade = maxGrade - topGrades + 1;
        return materiaOptions
            .Where(o => combatParams.Contains(o.ParamId) && o.Grade >= minGrade)
            .ToList();
    }

    /// <summary>Reads base stats from the Item sheet for a gear piece.</summary>
    public Dictionary<uint, int> CalcGearStats(uint itemId)
    {
        var stats = new Dictionary<uint, int>();
        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null || !itemSheet.TryGetRow(itemId, out var item)) return stats;

        for (int i = 0; i < item.BaseParam.Count && i < item.BaseParamValue.Count; i++)
        {
            var paramRef = item.BaseParam[i];
            if (!paramRef.IsValid || paramRef.RowId == 0) continue;
            var value = (int)item.BaseParamValue[i];
            if (value <= 0) continue;
            stats[paramRef.RowId] = value;
        }

        return stats;
    }

    /// <summary>
    /// Calculates gear base stats + materia with per-stat cap checking.
    /// Returns (totalStats, materiaDetails) where materiaDetails has per-materia info including capping.
    /// </summary>
    public (Dictionary<uint, int> Stats, List<MateriaDetail> MateriaDetails) CalcMeldedStats(uint itemId, List<uint>? materia)
    {
        var baseStats = CalcGearStats(itemId);
        var details = new List<MateriaDetail>();
        var meldedStats = new Dictionary<uint, int>(baseStats);

        if (materia == null || materia.Count == 0)
            return (meldedStats, details);

        EnsureCachesLoaded();

        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null || !itemSheet.TryGetRow(itemId, out var item))
            return (meldedStats, details);

        var itemLevel = (int)item.LevelItem.RowId;

        foreach (var matId in materia)
        {
            if (matId == 0) continue;
            var (paramId, value) = GetMateriaStatBonus(matId);
            if (paramId == 0) continue;

            int cap = GetStatCap(paramId, itemLevel, item);
            int baseValue = baseStats.TryGetValue(paramId, out var bv) ? bv : 0;
            int currentMelded = meldedStats.TryGetValue(paramId, out var cv) ? cv - baseValue : 0;
            int remaining = cap - baseValue - currentMelded;
            int effective = System.Math.Min(value, System.Math.Max(0, remaining));

            details.Add(new MateriaDetail
            {
                ItemId = matId,
                ParamId = paramId,
                StatName = GetStatName(paramId),
                RawValue = value,
                EffectiveValue = effective,
                IsCapped = effective < value
            });

            meldedStats[paramId] = (meldedStats.TryGetValue(paramId, out var existing) ? existing : 0) + effective;
        }

        return (meldedStats, details);
    }

    /// <summary>Calculates HQ food stat bonuses based on total stats (before food).</summary>
    public Dictionary<uint, int> CalcFoodStats(uint foodItemId, Dictionary<uint, int> totalStatsBeforeFood)
    {
        var foodStats = new Dictionary<uint, int>();
        if (foodItemId == 0) return foodStats;

        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null || !itemSheet.TryGetRow(foodItemId, out var foodItem)) return foodStats;

        if (!foodItem.ItemAction.IsValid) return foodStats;
        var action = foodItem.ItemAction.Value;

        if (action.DataHQ.Count < 2) return foodStats;
        var foodRowId = (uint)action.DataHQ[1];

        var foodSheet = dataManager.GetExcelSheet<ItemFood>();
        if (foodSheet == null || !foodSheet.TryGetRow(foodRowId, out var food)) return foodStats;

        foreach (var param in food.Params)
        {
            if (!param.BaseParam.IsValid || param.BaseParam.RowId == 0) continue;
            var paramId = param.BaseParam.RowId;
            var percent = (int)param.ValueHQ;
            var max = (int)param.MaxHQ;

            if (percent <= 0 && max <= 0) continue;

            int baseStat = totalStatsBeforeFood.TryGetValue(paramId, out var s) ? s : 0;
            int bonus = param.IsRelative ? System.Math.Min(baseStat * percent / 100, max) : max;

            if (bonus > 0)
                foodStats[paramId] = bonus;
        }

        return foodStats;
    }

    /// <summary>Gets food stat info for display (stat name, percent, max).</summary>
    public List<FoodStatInfo> GetFoodStatInfo(uint foodItemId)
    {
        var info = new List<FoodStatInfo>();
        if (foodItemId == 0) return info;

        EnsureCachesLoaded();

        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null || !itemSheet.TryGetRow(foodItemId, out var foodItem)) return info;

        if (!foodItem.ItemAction.IsValid) return info;
        var action = foodItem.ItemAction.Value;
        if (action.DataHQ.Count < 2) return info;
        var foodRowId = (uint)action.DataHQ[1];

        var foodSheet = dataManager.GetExcelSheet<ItemFood>();
        if (foodSheet == null || !foodSheet.TryGetRow(foodRowId, out var food)) return info;

        foreach (var param in food.Params)
        {
            if (!param.BaseParam.IsValid || param.BaseParam.RowId == 0) continue;
            if (param.ValueHQ <= 0 && param.MaxHQ <= 0) continue;

            info.Add(new FoodStatInfo
            {
                ParamId = param.BaseParam.RowId,
                StatName = GetStatName(param.BaseParam.RowId),
                Percent = param.ValueHQ,
                MaxHQ = param.MaxHQ,
                IsRelative = param.IsRelative
            });
        }

        return info;
    }

    /// <summary>Calculates total stats for an entire gear set + food.</summary>
    public Dictionary<uint, int> CalcFullSetStats(Dictionary<int, BiSItem> items, uint foodId)
    {
        var totals = new Dictionary<uint, int>();

        foreach (var kvp in items)
        {
            var (stats, _) = CalcMeldedStats(kvp.Value.ItemId, kvp.Value.Materia);
            foreach (var (paramId, value) in stats)
                totals[paramId] = (totals.TryGetValue(paramId, out var existing) ? existing : 0) + value;
        }

        var foodStats = CalcFoodStats(foodId, totals);
        foreach (var (paramId, value) in foodStats)
            totals[paramId] = (totals.TryGetValue(paramId, out var existing) ? existing : 0) + value;

        return totals;
    }

    // ── Cap Calculation ───────────────────────────────────────────

    private int GetStatCap(uint paramId, int itemLevel, Item item)
    {
        var bpSheet = dataManager.GetExcelSheet<BaseParam>();
        if (bpSheet == null || !bpSheet.TryGetRow(paramId, out var bp)) return int.MaxValue;

        int slotPercent = GetSlotPercent(bp, item);
        if (slotPercent <= 0) return int.MaxValue;

        var ilSheet = dataManager.GetExcelSheet<ItemLevel>();
        if (ilSheet == null || !ilSheet.TryGetRow((uint)itemLevel, out var ilRow)) return int.MaxValue;

        if (!ParamToItemLevel.TryGetValue(paramId, out var accessor)) return int.MaxValue;
        int statBudget = accessor(ilRow);

        return statBudget * slotPercent / 1000;
    }

    private static int GetSlotPercent(BaseParam bp, Item item)
    {
        if (!item.EquipSlotCategory.IsValid) return 0;
        var esc = item.EquipSlotCategory.Value;

        if (esc.MainHand != 0)
            return esc.OffHand > 0 ? bp.OneHandWeaponPercent : bp.TwoHandWeaponPercent;
        if (esc.OffHand > 0) return bp.OffHandPercent;
        if (esc.Head != 0) return bp.HeadPercent;
        if (esc.Body != 0) return bp.ChestPercent;
        if (esc.Gloves != 0) return bp.HandsPercent;
        if (esc.Legs != 0) return bp.LegsPercent;
        if (esc.Feet != 0) return bp.FeetPercent;
        if (esc.Ears != 0) return bp.EarringPercent;
        if (esc.Neck != 0) return bp.NecklacePercent;
        if (esc.Wrists != 0) return bp.BraceletPercent;
        if (esc.FingerR != 0 || esc.FingerL != 0) return bp.RingPercent;

        return 0;
    }

    // ── Data Types ────────────────────────────────────────────────

    public class MateriaDetail
    {
        public uint ItemId { get; set; }
        public uint ParamId { get; set; }
        public string StatName { get; set; } = string.Empty;
        public int RawValue { get; set; }
        public int EffectiveValue { get; set; }
        public bool IsCapped { get; set; }
    }

    public class FoodStatInfo
    {
        public uint ParamId { get; set; }
        public string StatName { get; set; } = string.Empty;
        public int Percent { get; set; }
        public int MaxHQ { get; set; }
        public bool IsRelative { get; set; }
    }
}
