using System;
using System.Collections.Generic;

namespace Understudy.Models;

public class EtroGearsetDetail
{
    public Dictionary<int, EtroGearsetSlot> Items { get; set; } = new();
    public uint FoodId { get; set; }
}

public class EtroGearsetSlot
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint ItemLevel { get; set; }
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
    public float patch { get; set; }
    public float gcd { get; set; }
    public List<System.Text.Json.JsonElement>? totalParams { get; set; }

    /// <summary>Average item level.</summary>
    public int AverageItemLevel { get; set; }
}
