using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using Understudy.Models;

namespace Understudy.Managers;

public unsafe class GearManager
{
    private readonly IDataManager dataManager;
    private readonly IObjectTable objectTable;

    public GearManager(IDataManager dataManager, IObjectTable objectTable)
    {
        this.dataManager = dataManager;
        this.objectTable = objectTable;
    }

    public GearSetData UpdateGear()
    {
        var player = objectTable[0] as IPlayerCharacter;
        if (player == null) return new GearSetData();

        var jobId = player.ClassJob.RowId;
        var manager = InventoryManager.Instance();
        if (manager == null) return new GearSetData { JobId = jobId };

        var gearData = new GearSetData
        {
            JobId = jobId,
            LastUpdated = DateTime.UtcNow,
            Items = new List<GearItem>()
        };

        var container = manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null) return gearData;

        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null) return gearData;

        for (int i = 0; i < container->Size; i++)
        {
            var itemSlot = container->GetInventorySlot(i);
            if (itemSlot == null || itemSlot->ItemId == 0) continue;

            var itemId = itemSlot->ItemId;

            if (itemSheet.TryGetRow(itemId, out var itemRow))
            {
                 if (i == 13) continue; // Soul Crystal

                 var materiaList = new List<uint>();
                 var materiaSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Materia>();

                 for (int m = 0; m < 5; m++)
                 {
                     var materiaId = itemSlot->GetMateriaId((byte)m);
                     if (materiaId == 0) continue;
                     
                    // MateriaGrade follows the 5-ushort Materia array (offset +10 bytes)
                    byte grade = 0;
                    fixed (ushort* pMateria = itemSlot->Materia)
                    {
                        grade = *(((byte*)pMateria) + 10 + m);
                    }

                    if (materiaSheet != null && materiaSheet.TryGetRow(materiaId, out var matRow))
                    {
                        if (grade < matRow.Item.Count)
                        {
                            var matItemId = matRow.Item[grade].Value.RowId;
                            materiaList.Add(matItemId);
                        }
                    }
                 }

                 gearData.Items.Add(new GearItem
                 {
                     ItemId = itemId,
                     Name = itemRow.Name.ToString(),
                     ItemLevel = itemRow.LevelItem.RowId, 
                     Slot = i,
                     Materia = materiaList
                 });
            }
        }

        if (gearData.Items.Count > 0)
        {
            gearData.AverageItemLevel = (float)gearData.Items.Average(x => x.ItemLevel);
        }

        return gearData;
    }
}
