using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Understudy.Managers;
using Understudy.Models;

namespace Understudy.Windows.Components;

public class UpgradePriority
{
    private readonly UpgradePriorityCalculator calculator;

    public UpgradePriority(UpgradePriorityCalculator calculator)
    {
        this.calculator = calculator;
    }

    /// <summary>
    /// Draws upgrade priority rows inside a job's loadout card.
    /// Expects comparisons and bisData already computed by the caller.
    /// </summary>
    public void DrawForJob(List<BiSSlotComparison> comparisons, BiSData bisData, uint jobId)
    {
        var suggestions = calculator.GetJobUpgradePriority(comparisons, bisData, jobId);

        if (suggestions.Count == 0) return;

        ImGui.Spacing();

        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float availW = ImGui.GetContentRegionAvail().X;
        dl.AddLine(pos, pos + new Vector2(availW, 0),
            ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), 1f);
        ImGui.Spacing();

        // Dynamic Column Sizing
        float colSlot = 76f;
        float colGain = 76f;

        float maxItemWidth = ImGui.CalcTextSize("ITEM").X;
        float maxAcqWidth = ImGui.CalcTextSize("SOURCE").X;

        foreach (var s in suggestions)
        {
            maxItemWidth = Math.Max(maxItemWidth, ImGui.CalcTextSize(s.ItemName).X);
            maxAcqWidth = Math.Max(maxAcqWidth, ImGui.CalcTextSize(s.AcquisitionLabel).X);
        }

        // Add padding
        float colItem = maxItemWidth + 32f;
        float colAcq  = maxAcqWidth + 32f;

        float totalWidth = colSlot + colItem + colAcq + colGain + 16f;
        float offsetX = 0f;
        if (totalWidth > availW)
        {
            float excess = totalWidth - availW;
            colItem -= excess / 2;
            colAcq -= excess / 2;
            totalWidth = availW;
        }
        else
        {
            offsetX = (availW - totalWidth) / 2f;
        }

        // Draw header
        float startX = ImGui.GetCursorPosX();

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentSecondary);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(FontAwesomeIcon.AngleDoubleUp.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextUnformatted("UPGRADE PRIORITY");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.SetCursorPosX(startX + offsetX);
        ImGui.TextUnformatted("SLOT");
        
        ImGui.SameLine(startX + offsetX + colSlot);
        DrawCenteredText("ITEM", colItem);
        
        ImGui.SameLine(startX + offsetX + colSlot + colItem);
        DrawCenteredText("SOURCE", colAcq);
        
        ImGui.SameLine(startX + offsetX + colSlot + colItem + colAcq);
        ImGui.TextUnformatted("EST. GAIN");
        ImGui.PopStyleColor();

        var headerLinePos = ImGui.GetCursorScreenPos();
        headerLinePos.X += offsetX;
        dl.AddLine(headerLinePos, headerLinePos + new Vector2(totalWidth, 0),
            ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.4f }), 1f);
        ImGui.Spacing();

        for (int i = 0; i < suggestions.Count; i++)
            DrawRow(suggestions[i], i, startX, offsetX, totalWidth, colSlot, colItem, colAcq, colGain);

        ImGui.Spacing();
    }

    private static void DrawRow(
        UpgradePriorityCalculator.UpgradeSuggestion s, int index,
        float startX, float offsetX, float totalWidth,
        float colSlot, float colItem, float colAcq, float colGain)
    {
        var rowBg  = index % 2 == 0 ? Theme.BgTableRow : Theme.BgTableRowAlt;
        var rowMin = ImGui.GetCursorScreenPos() - new Vector2(4, 1);
        rowMin.X += offsetX;
        var rowMax = rowMin + new Vector2(totalWidth + 8, ImGui.GetTextLineHeightWithSpacing() + 2);
        ImGui.GetWindowDrawList().AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(rowBg), Theme.RowRounding);

        var gainColor = s.DamageGainPercent >= 1.0
            ? Theme.AccentSuccess
            : s.DamageGainPercent >= 0.5
                ? Theme.AccentWarning
                : Theme.TextSecondary;

        ImGui.SetCursorPosX(startX + offsetX);
        ImGui.TextColored(Theme.TextSecondary, s.SlotName);
        
        ImGui.SameLine(startX + offsetX + colSlot);
        DrawCenteredText(TruncateText(s.ItemName, colItem), colItem, Theme.TextPrimary);
        
        ImGui.SameLine(startX + offsetX + colSlot + colItem);
        DrawCenteredText(TruncateText(s.AcquisitionLabel, colAcq), colAcq, Theme.TextSecondary);
        
        ImGui.SameLine(startX + offsetX + colSlot + colItem + colAcq);
        
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(gainColor, FontAwesomeIcon.ArrowUp.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextColored(gainColor, $"{s.DamageGainPercent:F2}%");

        ImGui.Spacing();
    }

    private static string TruncateText(string text, float maxWidth)
    {
        if (ImGui.CalcTextSize(text).X <= maxWidth) return text;
        while (text.Length > 3 && ImGui.CalcTextSize(text + "…").X > maxWidth)
            text = text[..^1];
        return text + "…";
    }

    private static void DrawCenteredText(string text, float columnWidth, Vector4? color = null)
    {
        float textWidth = ImGui.CalcTextSize(text).X;
        float offsetX = (columnWidth - textWidth) / 2f;
        if (offsetX < 0) offsetX = 0;

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        
        if (color.HasValue)
            ImGui.TextColored(color.Value, text);
        else
            ImGui.TextUnformatted(text);
        
        // Advance cursor past the column width to maintain correct spacing for the next item
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - offsetX - textWidth + columnWidth);
    }
}
