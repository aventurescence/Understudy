using System;
using System.Collections.Generic;

namespace Understudy.Models;

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

    /// <summary>Expected food for this BiS.</summary>
    public uint FoodId { get; set; }
}
