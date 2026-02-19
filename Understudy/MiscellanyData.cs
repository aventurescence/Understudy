using System;
using System.Collections.Generic;

namespace Understudy;

/// <summary>
/// Raid miscellany data: books per floor, upgrade materials, coffers.
/// Updated by scanning player inventory, saddlebag, and retainers.
/// </summary>
[Serializable]
public class MiscellanyData
{
    /// <summary>Number of books per floor. Key = "M1".."M4", value = total count.</summary>
    public Dictionary<string, int> BookCounts { get; set; } = new();

    public int TwineCount { get; set; }
    public int GlazeCount { get; set; }
    public int SolventCount { get; set; }
    public int UniversalTomestoneCount { get; set; }

    /// <summary>Unopened coffers from the current tier. Key = item name, value = count.</summary>
    public Dictionary<string, int> CofferCounts { get; set; } = new();

    public DateTime LastUpdated { get; set; }
}
