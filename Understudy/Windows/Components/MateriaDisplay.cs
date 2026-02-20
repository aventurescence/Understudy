using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// Renders materia comparison icons (current vs BiS) with match/mismatch/ghost states.
/// </summary>
public class MateriaDisplay
{
    private readonly Plugin plugin;

    public MateriaDisplay(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void DrawMateriaRow(List<uint>? currentMateria, List<uint>? bisMateria)
    {
        if (currentMateria == null && bisMateria == null) return;

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        float size = 20f;
        var spacing = 2f;
        var startPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        var currentList = currentMateria?.Where(x => x != 0).ToList() ?? new List<uint>();
        var neededList = bisMateria?.Where(x => x != 0).ToList() ?? new List<uint>();

        var expectedNames = neededList.Select(id => sheet.TryGetRow(id, out var r) ? r.Name.ToString() : $"#{id}").ToList();
        var expectedTooltip = expectedNames.Count > 0 ? "Expected one of:\n" + string.Join("\n", expectedNames.Select(n => $"- {n}")) : "No materia expected";

        int drawIndex = 0;

        foreach (var matId in currentList)
        {
            bool isCorrect = neededList.Remove(matId);
            bool isMismatch = !isCorrect;

            DrawMateriaIcon(drawList, sheet, startPos, drawIndex, matId, isGhost: false, isError: isMismatch, isValid: isCorrect, isMissing: false, expectedTooltip: isMismatch ? expectedTooltip : null);
            drawIndex++;
        }

        foreach (var missingId in neededList)
        {
            DrawMateriaIcon(drawList, sheet, startPos, drawIndex, missingId, isGhost: true, isError: true, isValid: false, isMissing: true, expectedTooltip: null);
            drawIndex++;
        }

        ImGui.Dummy(new Vector2(Math.Max(5, drawIndex) * (size + spacing), size));
    }

    public void DrawMateriaOnly(List<uint>? materia)
    {
        float size = 20f;
        var spacing = 2f;

        if (materia == null)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        var filtered = materia.Where(x => x != 0).ToList();
        if (filtered.Count == 0)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        var startPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        int drawIndex = 0;
        foreach (var matId in filtered)
        {
            DrawMateriaIcon(drawList, sheet, startPos, drawIndex, matId, isGhost: false, isError: false, isValid: false, isMissing: false, expectedTooltip: null);
            drawIndex++;
        }

        ImGui.Dummy(new Vector2(drawIndex * (size + spacing), size));
    }

    public void DrawMateriaIndicators(List<uint>? materia)
    {
        if (materia == null || !materia.Any(m => m > 0)) return;

        ImGui.SameLine(0, 4);
        foreach (var m in materia.Where(x => x > 0))
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 0.8f), "●");
            ImGui.SameLine(0, 2);
        }
    }

    private void DrawMateriaIcon(ImDrawListPtr drawList, Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Item> sheet, Vector2 startPos, int index, uint matId, bool isGhost, bool isError, bool isValid, bool isMissing, string? expectedTooltip)
    {
        if (!sheet.TryGetRow(matId, out var itemRow)) return;

        float size = 20f;
        var spacing = 2f;

        IDalamudTextureWrap? iconWrap = null;

        int grade = plugin.MateriaTextures.GetGradeForItem(matId);
        if (grade >= 0)
        {
            iconWrap = plugin.MateriaTextures.GetIcon(grade);
        }

        if (iconWrap == null)
        {
            var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(itemRow.Icon));
            if (iconTex.TryGetWrap(out var w, out _)) iconWrap = w;
        }

        if (iconWrap != null)
        {
            var pMin = startPos + new Vector2(index * (size + spacing), 0);
            var pMax = pMin + new Vector2(size, size);

            uint color = isGhost ? ImGui.GetColorU32(new Vector4(1, 1, 1, 0.4f)) : 0xFFFFFFFF;
            drawList.AddImage(iconWrap.Handle, pMin, pMax, Vector2.Zero, Vector2.One, color);

            if (isError)
            {
                drawList.AddRect(pMin, pMax, ImGui.GetColorU32(Theme.AccentDanger), 0f, ImDrawFlags.None, 2f);
            }

            if (ImGui.IsMouseHoveringRect(pMin, pMax))
            {
                ImGui.BeginTooltip();
                string status = isError ? (isMissing ? "(Missing)" : "(Extra/Incorrect)") : "";

                ImGui.TextColored(isValid ? Theme.AccentSuccess : (isError ? Theme.AccentDanger : Theme.TextPrimary), itemRow.Name.ToString());

                if (!string.IsNullOrEmpty(status))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Theme.TextSecondary, status);
                }

                if (!string.IsNullOrEmpty(expectedTooltip))
                {
                    ImGui.Spacing();
                    ImGui.TextColored(Theme.AccentWarning, expectedTooltip);
                }

                ImGui.Image(iconWrap.Handle, new Vector2(40, 40));
                ImGui.EndTooltip();
            }
        }
    }
}
