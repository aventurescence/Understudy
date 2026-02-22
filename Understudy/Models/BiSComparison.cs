using System.Collections.Generic;

namespace Understudy.Models;

/// <summary>
/// Aggregated acquisition costs to complete a BiS set from scratch.
/// Populated by BiSComparisonManager when comparing current gear against a BiS set.
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
