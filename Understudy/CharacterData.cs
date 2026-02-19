using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Understudy;

[Serializable]
public class CharacterData
{
    public string Name { get; set; } = string.Empty;
    public uint WorldId { get; set; }
    public ulong ContentId { get; set; }

    public TomestoneData Tomestones { get; set; } = new();
    public RaidData RaidProgress { get; set; } = new();
    public Dictionary<uint, GearSetData> GearSets { get; set; } = new(); // Keyed by Job ID
    public Dictionary<uint, BiSData> BisSets { get; set; } = new(); // Keyed by Job ID
    public MiscellanyData Miscellany { get; set; } = new();
}

[Serializable]
public class RaidData
{
    // Raid Tier (e.g. Arcadion Savage M1-M4)
    // We can store boolean flags for each floor/turn.
    public bool M1 { get; set; }
    public bool M2 { get; set; }
    public bool M3 { get; set; }
    public bool M4 { get; set; }
    
    public DateTime LastUpdated { get; set; }
}

[Serializable]
public class GearSetData
{
    public uint JobId { get; set; }
    public float AverageItemLevel { get; set; }
    public List<GearItem> Items { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

[Serializable]
public class GearItem
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint ItemLevel { get; set; }
    [JsonConverter(typeof(SlotConverter))]
    public int Slot { get; set; }

    /// <summary>Up to 5 materia item IDs melded into this item. 0 = empty slot.</summary>
    public List<uint> Materia { get; set; } = new();
}

[Serializable]
public class TomestoneData
{
    // Mnemonics (Weekly Capped)
    // Mathematics
    
    public int Mnemonics { get; set; }
    public int MnemonicsWeekly { get; set; }
    public int MnemonicsCap { get; set; } = 2000;
    public int MnemonicsWeeklyCap { get; set; } = 450;

    public int Mathematics { get; set; }
    public int MathematicsWeekly { get; set; }
    public int MathematicsCap { get; set; } = 2000;

    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Backward-compatible JSON converter for GearItem.Slot migration from string to int.
/// Reads both "0" (old format) and 0 (new format) during deserialization.
/// </summary>
public class SlotConverter : JsonConverter<int>
{
    public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        return reader.TokenType == JsonToken.String
            ? int.Parse((string)reader.Value!)
            : Convert.ToInt32(reader.Value);
    }

    public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }
}
