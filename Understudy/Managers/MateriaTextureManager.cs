using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Understudy.Managers;

/// <summary>
/// Loads and manages the stylized materia icons from the game's UI ULD file.
/// Based on logic from CopeSeetheMeld: https://github.com/xanunderscore/CopeSeetheMeld/blob/master/CopeSeetheMeld/UI/MeldUI.cs
/// </summary>
public class MateriaTextureManager : IDisposable
{
    // The ULD wrapper handles loading texture parts from the game UI logic
    // CopeSeetheMeld uses: Plugin.PluginInterface.UiBuilder.LoadUld("ui/uld/ItemDetail.uld")
    // Note: In modern Dalamud, verify if UldWrapper exists or if we need to access via UiBuilder.
    // The previous code snippet used: var materiaUld = Plugin.PluginInterface.UiBuilder.LoadUld("ui/uld/ItemDetail.uld");
    // And method: LoadTexturePart("ui/uld/ItemDetail_hr1.tex", partId)
    
    // Grades 1-12 Normal
    private static readonly int[] IconParts = [6, 5, 4, 3, 21, 23, 25, 27, 29, 31, 33, 35];
    
    // Grades 1-12 Overmeld (though we might not need overmeld visuals specifically yet, keeping for completeness)
    private static readonly int[] IconOvermeldParts = [20, 19, 18, 17, 22, 24, 26, 28, 30, 32, 34, 36];

    private readonly List<IDalamudTextureWrap?> materiaIcons = new();
    private dynamic? uldWrapper; // Using dynamic to avoid type dependency issues until confirmed
    private readonly Dictionary<uint, int> itemGradeLookup = new();

    public MateriaTextureManager(Plugin plugin)
    {
        try
        {
            // Attempt to load ULD wrapper safely using reflection to avoid runtime crashes if API changes
            var uiBuilder = Plugin.PluginInterface.UiBuilder;
            var loadUldMethod = uiBuilder.GetType().GetMethod("LoadUld");
            
            if (loadUldMethod != null)
            {
                uldWrapper = loadUldMethod.Invoke(uiBuilder, new object[] { "ui/uld/ItemDetail.uld" });
                
                if (uldWrapper != null)
                {
                    var loadTexPartMethod = uldWrapper.GetType().GetMethod("LoadTexturePart", new[] { typeof(string), typeof(int) });

                    if (loadTexPartMethod != null)
                    {
                        foreach (var partId in IconParts)
                        {
                            var tex = (IDalamudTextureWrap?)loadTexPartMethod.Invoke(uldWrapper, new object[] { "ui/uld/ItemDetail_hr1.tex", partId });
                            materiaIcons.Add(tex);
                        }
                    }
                }
            }
            
            // If failed to load stylized icons, we will just have empty list and fallback later or use standard icons
            if (materiaIcons.Count == 0)
            {
                Plugin.Log.Warning("Could not load stylized materia icons from ULD. Using fallbacks if available.");
            }

            // Build Grade Lookup
            var matSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Materia>();
            if (matSheet != null)
            {
                foreach (var row in matSheet)
                {
                     for(int g=0; g<row.Item.Count; g++)
                     {
                         var val = row.Item[g].Value;
                         if (val.RowId != 0) itemGradeLookup[val.RowId] = g;
                     }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to load materia textures from ULD.");
        }
    }

    public IDalamudTextureWrap? GetIcon(int grade)
    {
        if (grade < 0 || grade >= materiaIcons.Count) return null;
        return materiaIcons[grade];
    }

    public int GetGradeForItem(uint itemId)
    {
        return itemGradeLookup.TryGetValue(itemId, out var g) ? g : -1;
    }

    public void Dispose()
    {
        foreach (var icon in materiaIcons)
        {
            icon?.Dispose();
        }
        materiaIcons.Clear();
        
        if (uldWrapper != null)
        {
            try { ((IDisposable)uldWrapper).Dispose(); } catch { }
        }
    }
}
