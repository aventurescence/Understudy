using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using Understudy.Models;

namespace Understudy.Managers;

public unsafe class RaidManager
{
    private readonly IDataManager dataManager;
    private const int WeeklyRaidsOffset = 0x5F0; // PlayerState.WeeklyLockoutInfo

    private readonly Dictionary<string, uint> raidIds = new();
    private readonly Dictionary<string, uint> raidImageIds = new();
    private readonly string[] targetRaids = TierConfig.RaidNames;

    public RaidManager(IDataManager dataManager)
    {
        this.dataManager = dataManager;
        InitializeIds();
    }

    private void InitializeIds()
    {
        var sheet = dataManager.GetExcelSheet<ContentFinderCondition>();
        if (sheet == null) return;

        foreach (var raidName in targetRaids)
        {
            var row = sheet.FirstOrDefault(x => x.Name.ToString() == raidName);
            if (row.RowId != 0)
            {
                raidIds[raidName] = row.RowId;
                raidImageIds[raidName] = row.Image;
            }
        }
    }

    /// <summary>
    /// Gets the Icon ID of the raid image for UI display.
    /// </summary>
    public uint GetRaidImageId(string raidName)
    {
        return raidImageIds.TryGetValue(raidName, out var imageId) ? imageId : 0;
    }

    /// <summary>
    /// Gets all raid Icon IDs indexed by M1-M4.
    /// </summary>
    public Dictionary<string, uint> GetAllRaidImageIds()
    {
        var result = new Dictionary<string, uint>();
        for (int i = 0; i < targetRaids.Length; i++)
        {
            var key = $"M{i + 1}";
            result[key] = raidImageIds.TryGetValue(targetRaids[i], out var id) ? id : 0;
        }
        return result;
    }

    public unsafe RaidData GetRaidDataFromLockoutInfo()
    {
        var data = new RaidData { LastUpdated = DateTime.UtcNow };
        var playerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
        
        if (playerState == null) return data;
        if (!raidIds.TryGetValue(targetRaids[0], out var m1CfcId)) return data;

        // WeeklyLockoutInfo bitfield: M1=bit2, M2=bit4, M3=bit3, M4=bit5
        var bitMapping = new int[] { 2, 4, 3, 5 };
        var lockoutBase = (byte*)playerState + WeeklyRaidsOffset;

        bool ReadLockout(string raidName, int floorIndex)
        {
            if (floorIndex < 0 || floorIndex >= bitMapping.Length) return false;

            int bitIndex = bitMapping[floorIndex];
            int byteOffset = bitIndex / 8;
            int bitOffset = bitIndex % 8;
            byte val = *(lockoutBase + byteOffset);
            bool cleared = (val & (1 << bitOffset)) != 0;

            Plugin.Log.Debug("{Raid}: bitIdx={BitIndex} -> byte[{Byte}]=0x{Val:X2} bit{Bit}={Cleared}",
                raidName, bitIndex, byteOffset, val, bitOffset, cleared);

            return cleared;
        }

        data.M1 = ReadLockout(targetRaids[0], 0);
        data.M2 = ReadLockout(targetRaids[1], 1);
        data.M3 = ReadLockout(targetRaids[2], 2);
        data.M4 = ReadLockout(targetRaids[3], 3);
        
        return data;
    }
}
