using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

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
        // IObjectTable[0] is always the local player
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

        // Equip slots: 0=MainHand, 1=OffHand, 2=Head, 3=Body, 4=Hands, 5=Waist(Deleted), 6=Legs, 7=Feet, 8=Ears, 9=Neck, 10=Wrists, 11=RRing, 12=LRing
        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null) return gearData;

        for (int i = 0; i < container->Size; i++)
        {
            var itemSlot = container->GetInventorySlot(i);
            if (itemSlot == null || itemSlot->ItemId == 0) continue;

            var itemId = itemSlot->ItemId;

            if (itemSheet.TryGetRow(itemId, out var itemRow))
            {
                 // Filter out Soul Crystal (Slot 13)
                 if (i == 13) continue;

                 var materiaList = new List<uint>();
                 var materiaSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Materia>();

                 for (int m = 0; m < 5; m++)
                 {
                     var materiaId = itemSlot->GetMateriaId((byte)m);
                     if (materiaId == 0) continue;
                     
                    // Revert to pointer arithmetic as FFXIVClientStructs InventoryItem structure 
                    // does not expose MateriaGrade directly.
                    // Materia array is 5 ushorts (10 bytes). MateriaGrade array follows immediately.
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

        // Calculate Avg IL
        if (gearData.Items.Count > 0)
        {
            // Simple average logic (ignore soul crystal?)
            // This is an approximation. Real game uses specific formula.
            // We can read PlayerState.Instance()->ItemLevel directly?
            // Yes, ClientState.LocalPlayer.ItemLevel? No, explicit prop might not exist on object?
            // Actually it might just be easier to read from the UI or recalculate.
            // Let's recalculate simply for now.
            gearData.AverageItemLevel = (float)gearData.Items.Average(x => x.ItemLevel);
        }

        return gearData;
    }
}
