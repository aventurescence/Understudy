using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Understudy.Models;

namespace Understudy.Managers;

/// <summary>
/// Ranks missing BiS items by estimated % damage multiplier gain
/// using FFXIV level 100 combat formulas (source: xiv-gear-planner).
/// Includes job-specific main stat scaling via ClassJob.PrimaryStat and Modifier* fields.
/// </summary>
public class UpgradePriorityCalculator
{
    // Level 100 constants (verified via xiv-gear-planner/xivconstants.ts)
    private const double BaseSubStat  = 420.0;
    private const double BaseMainStat = 440.0;
    private const double LevelDiv     = 2780.0;
    private const double MainStatPowerMod = 237.0; // non-tank jobs at level 100

    private readonly StatCalculator statCalculator;
    private readonly IDataManager dataManager;

    public record UpgradeSuggestion(
        int SlotId,
        string SlotName,
        string ItemName,
        string AcquisitionLabel,
        double DamageGainPercent);

    public UpgradePriorityCalculator(StatCalculator statCalculator, IDataManager dataManager)
    {
        this.statCalculator = statCalculator;
        this.dataManager = dataManager;
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>
    /// Given already-computed comparisons for a single job, returns unowned BiS items
    /// sorted by estimated % damage gain descending.
    /// </summary>
    public List<UpgradeSuggestion> GetJobUpgradePriority(
        List<BiSSlotComparison> comparisons,
        BiSData bisData,
        uint jobId)
    {
        var unowned = comparisons.Where(c => !c.IsOwned && c.BiSItem != null).ToList();
        if (unowned.Count == 0) return [];

        var (primaryStatParamId, jobModifier, isTank) = GetJobStatInfo(jobId);

        // Build effective current set: BiS where owned, current gear where not
        var currentSetItems = new Dictionary<int, BiSItem>();
        foreach (var comp in comparisons)
        {
            if (comp.IsOwned && comp.BiSItem != null)
            {
                currentSetItems[comp.SlotId] = comp.BiSItem;
            }
            else if (comp.CurrentItem != null)
            {
                currentSetItems[comp.SlotId] = new BiSItem
                {
                    ItemId  = comp.CurrentItem.ItemId,
                    Slot    = comp.CurrentItem.Slot,
                    Materia = comp.CurrentItem.Materia,
                };
            }
        }

        double baseFactor = CalcDamageFactor(
            statCalculator.CalcFullSetStats(currentSetItems, bisData.FoodId),
            primaryStatParamId, jobModifier, isTank);

        var suggestions = new List<UpgradeSuggestion>();
        foreach (var comp in unowned)
        {
            var trialSet = new Dictionary<int, BiSItem>(currentSetItems)
            {
                [comp.SlotId] = comp.BiSItem!
            };

            double trialFactor = CalcDamageFactor(
                statCalculator.CalcFullSetStats(trialSet, bisData.FoodId),
                primaryStatParamId, jobModifier, isTank);

            double gainPercent = (trialFactor / baseFactor - 1.0) * 100.0;

            suggestions.Add(new UpgradeSuggestion(
                SlotId:           comp.SlotId,
                SlotName:         SlotIdToName(comp.SlotId),
                ItemName:         comp.BiSItem!.Name,
                AcquisitionLabel: comp.AcquisitionLabel,
                DamageGainPercent: gainPercent));
        }

        return suggestions.OrderByDescending(s => s.DamageGainPercent).ToList();
    }

    // ── Job Stat Lookup ──────────────────────────────────────────

    /// <summary>
    /// Returns (primaryStatParamId, jobModifier %, isTank) for a given job.
    /// PrimaryStat in ClassJob: 1=STR, 2=DEX, 3=VIT, 4=INT, 5=MND.
    /// Modifier* fields are percentages (e.g. 105 = 105%).
    /// </summary>
    private (uint primaryStatParamId, int jobModifier, bool isTank) GetJobStatInfo(uint jobId)
    {
        var sheet = dataManager.GetExcelSheet<ClassJob>();
        if (sheet == null || !sheet.TryGetRow(jobId, out var job))
            return (1, 100, false); // fallback: STR, 100%, non-tank

        bool isTank = job.Role == 1;

        // PrimaryStat: 1=STR, 2=DEX, 4=INT, 5=MND (maps to ParamIds 1,2,4,5)
        uint paramId = job.PrimaryStat switch
        {
            1 => 1u,  // STR
            2 => 2u,  // DEX
            4 => 4u,  // INT
            5 => 5u,  // MND
            _ => 1u   // fallback STR
        };

        int modifier = job.PrimaryStat switch
        {
            1 => (int)job.ModifierStrength,
            2 => (int)job.ModifierDexterity,
            4 => (int)job.ModifierIntelligence,
            5 => (int)job.ModifierMind,
            _ => 100
        };

        return (paramId, modifier, isTank);
    }

    // ── FFXIV Level 100 Damage Formulas ──────────────────────────
    // Reference: https://github.com/xiv-gear-planner/gear-planner (xivmath.ts)

    private static double CalcMainStatFactor(int mainStat, int jobModifier, bool isTank)
    {
        // Apply job modifier to base main stat: floor(baseMainStat * modifier / 100)
        double jobBase = Math.Floor(BaseMainStat * jobModifier / 100.0);
        double powerMod = isTank ? 190.0 : MainStatPowerMod;
        return (Math.Truncate(powerMod * (mainStat - jobBase) / jobBase) + 100.0) / 100.0;
    }

    private static double CalcCritFactor(int crt)
    {
        double delta    = crt - BaseSubStat;
        double critRate = (200.0 * delta / LevelDiv + 50.0) / 1000.0;
        double critDmg  = (1400.0 + 200.0 * delta / LevelDiv) / 1000.0;
        return 1.0 + critRate * (critDmg - 1.0);
    }

    private static double CalcDetFactor(int det)
    {
        return (1000.0 + 140.0 * (det - BaseSubStat) / LevelDiv) / 1000.0;
    }

    private static double CalcDhFactor(int dh)
    {
        double dhRate = (550.0 * (dh - BaseSubStat) / LevelDiv) / 1000.0;
        return 1.0 + 0.25 * dhRate;
    }

    private static double CalcDamageFactor(
        Dictionary<uint, int> stats,
        uint primaryStatParamId,
        int jobModifier,
        bool isTank)
    {
        // ParamIds: 27=CRT, 44=DET, 22=DH
        int mainStat = stats.TryGetValue(primaryStatParamId, out var ms) ? ms : (int)BaseMainStat;
        int crt      = stats.TryGetValue(27, out var c) ? c : (int)BaseSubStat;
        int det      = stats.TryGetValue(44, out var d) ? d : (int)BaseSubStat;
        int dh       = stats.TryGetValue(22, out var h) ? h : (int)BaseSubStat;

        return CalcMainStatFactor(mainStat, jobModifier, isTank)
             * CalcCritFactor(crt)
             * CalcDetFactor(det)
             * CalcDhFactor(dh);
    }

    private static string SlotIdToName(int slotId) => slotId switch
    {
        0  => "Weapon",
        1  => "Off-Hand",
        2  => "Head",
        3  => "Body",
        4  => "Hands",
        6  => "Legs",
        7  => "Feet",
        8  => "Earrings",
        9  => "Necklace",
        10 => "Bracelet",
        11 => "Ring (L)",
        12 => "Ring (R)",
        _  => $"Slot {slotId}"
    };
}
