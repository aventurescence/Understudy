using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Understudy.Models;

namespace Understudy.Managers;

public unsafe class TomestoneManager
{
    private readonly IDataManager dataManager;
    
    // TomestonesItem sheet Row IDs (not Item sheet IDs)
    private const uint MnemonicsId = 49;
    private const uint MathematicsId = 48;

    public TomestoneManager(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    public TomestoneData UpdateTomestones()
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return new TomestoneData();

        var data = new TomestoneData
        {
            LastUpdated = DateTime.UtcNow
        };

        data.Mnemonics = (int)manager->GetTomestoneCount(MnemonicsId);
        data.MnemonicsCap = 2000;
        data.MnemonicsWeekly = manager->GetWeeklyAcquiredTomestoneCount();
        data.MnemonicsWeeklyCap = InventoryManager.GetLimitedTomestoneWeeklyLimit();

        data.Mathematics = (int)manager->GetTomestoneCount(MathematicsId);
        data.MathematicsCap = 2000;
        data.MathematicsWeekly = 0;

        return data;
    }
}
