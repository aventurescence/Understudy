using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Understudy.Managers;

public unsafe class TomestoneManager
{
    private readonly IDataManager dataManager;
    
    // Tomestone Item IDs (Row IDs from the TomestonesItem sheet, NOT the Item sheet)
    // 49 = Mnemonics (Weekly Capped)
    // 48 = Mathematics (Uncapped)
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

        // Mnemonics (Capped) - Use GetTomestoneCount with the item ID
        data.Mnemonics = (int)manager->GetTomestoneCount(MnemonicsId);
        data.MnemonicsCap = 2000;
        data.MnemonicsWeekly = manager->GetWeeklyAcquiredTomestoneCount();
        
        // Get the actual weekly limit from the game instead of hardcoding 450
        data.MnemonicsWeeklyCap = InventoryManager.GetLimitedTomestoneWeeklyLimit();

        // Mathematics (Uncapped)
        data.Mathematics = (int)manager->GetTomestoneCount(MathematicsId);
        data.MathematicsCap = 2000;
        data.MathematicsWeekly = 0;

        return data;
    }
}
