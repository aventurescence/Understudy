using System;
using System.Collections.Generic;

namespace Understudy;

/// <summary>
/// Identifies the acquisition source of a gear item.
/// </summary>
public enum GearSource
{
    Unknown,
    Savage,              // "Grand Champion" prefix — dropped from savage raids
    Tomestone,           // "Bygone" prefix — purchased with Mnemonics (iL780)
    AugmentedTomestone,  // "Augmented Bygone" prefix — upgraded tome gear (iL790)
    Crafted,             // HQ crafted gear
}

/// <summary>
/// A single item in a Best In Slot gearset, including materia melds.
/// </summary>
[Serializable]
public class BiSItem
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint ItemLevel { get; set; }

    /// <summary>Internal equipment slot ID (0=MH, 1=OH, 2=Head, etc.).</summary>
    public int Slot { get; set; }

    /// <summary>How this item is acquired.</summary>
    public GearSource Source { get; set; } = GearSource.Unknown;

    /// <summary>Which savage floor drops this item (e.g. "M1"–"M4"), if Source is Savage.</summary>
    public string FloorSource { get; set; } = string.Empty;

    /// <summary>Up to 5 materia item IDs. 0 = empty slot.</summary>
    public List<uint> Materia { get; set; } = new() { 0, 0, 0, 0, 0 };
}

/// <summary>
/// A complete Best In Slot gearset for a specific job, imported from an external source or manually entered.
/// </summary>
[Serializable]
public class BiSData
{
    public uint JobId { get; set; }

    /// <summary>Import source: "etro", "xivgear", or "manual".</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Original URL used for import (empty for manual entry).</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Display name from the imported gearset.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Items keyed by internal slot ID (0=MH, 2=Head, etc.).</summary>
    public Dictionary<int, BiSItem> Items { get; set; } = new();

    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Aggregated acquisition costs to complete a BiS set from scratch.
/// Populated by BiSManager when comparing current gear against a BiS set.
/// </summary>
public class AcquisitionCosts
{
    /// <summary>Total Allagan Tomestones of Mnemonics needed for all missing tome pieces.</summary>
    public int TomestonesNeeded { get; set; }

    /// <summary>Universal Tomestone 3.0 needed for the weapon (0 or 1).</summary>
    public int UniversalTomestonesNeeded { get; set; }

    /// <summary>Books needed per savage floor. Key = "M1"–"M4", Value = count.</summary>
    public Dictionary<string, int> BooksNeeded { get; set; } = new()
    {
        ["M1"] = 0, ["M2"] = 0, ["M3"] = 0, ["M4"] = 0
    };

    /// <summary>Thundersteeping Twine needed (left-side armor upgrades).</summary>
    public int TwineNeeded { get; set; }

    /// <summary>Thundersteeping Glaze needed (right-side accessory upgrades).</summary>
    public int GlazeNeeded { get; set; }

    /// <summary>Thundersteeping Solvent needed (weapon upgrade).</summary>
    public int SolventNeeded { get; set; }
}

/// <summary>
/// Result of comparing a player's current gear against their BiS set for a specific slot.
/// </summary>
public class BiSSlotComparison
{
    public int SlotId { get; set; }
    public BiSItem? BiSItem { get; set; }
    public GearItem? CurrentItem { get; set; }

    /// <summary>True if the currently equipped item matches the BiS item.</summary>
    public bool IsOwned { get; set; }

    /// <summary>Human-readable label for how to acquire this piece (e.g. "M2 Savage", "495 Tomes").</summary>
    public string AcquisitionLabel { get; set; } = string.Empty;
}

public class EtroBiSSet
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public int job { get; set; } // Job ID
    public int minItemLevel { get; set; }
    public int maxItemLevel { get; set; }
    public string creator { get; set; } = string.Empty;
    public DateTime userUpdatedAt { get; set; }
}
