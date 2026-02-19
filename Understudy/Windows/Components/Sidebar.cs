using System;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

public class Sidebar
{
    private readonly Plugin plugin;
    private readonly Action<ulong?> onSelectionChanged;
    private readonly Action onConfigRequested;
    private ulong? selectedContentId;
    private readonly ISharedImmediateTexture? logoTexture;

    public Sidebar(Plugin plugin, Action<ulong?> onSelectionChanged, Action onConfigRequested)
    {
        this.plugin = plugin;
        this.onSelectionChanged = onSelectionChanged;
        this.onConfigRequested = onConfigRequested;

        var assemblyDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
        if (assemblyDir != null)
        {
            var logoPath = Path.Combine(assemblyDir, "aSSETS", "Understudy1.png");
            if (File.Exists(logoPath))
                logoTexture = Plugin.TextureProvider.GetFromFile(logoPath);
        }
    }

    public void UpdateSelection(ulong? contentId)
    {
        selectedContentId = contentId;
    }

    public void Draw()
    {
        var drawList = ImGui.GetWindowDrawList();
        
        ImGui.Spacing();
        ImGui.Indent(12);

        // Logo
        // Increased size as requested for better branding presence
        if (logoTexture != null && logoTexture.TryGetWrap(out var logoWrap, out _))
        {
            var logoSize = 128f * ImGui.GetIO().FontGlobalScale; // Increased from 64f
            var availWidth = ImGui.GetContentRegionAvail().X;
            // Center the logo
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - logoSize) * 0.5f - 12);
            ImGui.Image(logoWrap.Handle, new Vector2(logoSize, logoSize));
        }
        else
        {
            // Fallback text if logo missing (though should be rare)
            // User requested removing text, but only if logo exists? 
            // "Remove this text" likely referred to the header text I proposed adding/enhancing.
            // But if logo is missing, we need SOMETHING. I'll keep a small fallback or empty.
            // I'll leave a small spacer.
            ImGui.Spacing();
        }

        ImGui.Unindent(12);

        // Decorative line under the logo area
        ImGui.Dummy(new Vector2(0, 10)); // Spacer
        var lineStart = ImGui.GetCursorScreenPos();
        var lineWidth = ImGui.GetContentRegionAvail().X;
        drawList.AddLine(
            lineStart,
            lineStart + new Vector2(lineWidth, 0),
            ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.4f }),
            1.5f);

        ImGui.Spacing();
        ImGui.Spacing();

        // Dashboard Item
        DrawSidebarItem(null, "Dashboard", FontAwesomeIcon.ChartLine, selectedContentId == null);

        ImGui.Spacing();
        ImGui.Spacing();

        // "Characters" Section
        ImGui.TextColored(Theme.TextDisabled, "  CHARACTERS");
        ImGui.Spacing();

        foreach (var kvp in plugin.Configuration.Characters)
        {
            var id = kvp.Key;
            var data = kvp.Value;
            var isSelected = selectedContentId == id;

            DrawSidebarItem(id, data.Name, FontAwesomeIcon.User, isSelected, GetWorldName(data.WorldId));
        }

        // Actions at bottom of sidebar
        var remaining = ImGui.GetContentRegionAvail().Y;
        if (remaining > 50)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + remaining - 50);

            // Decorative separator
            var sepStart = ImGui.GetCursorScreenPos();
            drawList.AddLine(
                sepStart,
                sepStart + new Vector2(lineWidth, 0),
                ImGui.GetColorU32(Theme.BorderSubtle),
                1.0f);
            ImGui.Spacing();

            // Settings using the helper (no ID, special action)
            // We pass isSelected = false because we don't have a specific ID for settings here in this loop
            // asking the sidebar who is selected. 
            // Ideally Sidebar should know if "Settings" is selected.
            // But for now, we can check if selectedContentId is null AND we are in settings mode?
            // Since we don't pass "view mode" to sidebar, we can't highlight it easily without changing signature.
            // I'll leave highlight off for now or check if I can infer it.
            // Actually, I'll update Sidebar class to track "Settings" selection if I want to highlight it.
            // But keeping it simple:
            DrawSidebarItem(99999, "Settings", FontAwesomeIcon.Cog, false, null, true);
        }
    }

    private void DrawSidebarItem(ulong? id, string label, FontAwesomeIcon icon, bool isSelected, string? subtext = null, bool isSettings = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        // height depends on subtext
        float height = subtext != null ? 38f : 30f; 

        // Hitbox for click
        ImGui.InvisibleButton($"##Sidebar_{label}_{id}", new Vector2(width, height));
        bool isHovered = ImGui.IsItemHovered();
        bool isClicked = ImGui.IsItemClicked();

        if (isClicked)
        {
            if (isSettings)
                onConfigRequested();
            else
            {
                selectedContentId = id;
                onSelectionChanged(id);
            }
        }

        // Background (Selected or Hover)
        if (isSelected)
        {
            drawList.AddRectFilled(startPos, startPos + new Vector2(width, height),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.15f }), 4f);
                
            // Left accent bar
            drawList.AddRectFilled(startPos + new Vector2(0, 4), startPos + new Vector2(3, height - 4),
                 ImGui.GetColorU32(Theme.AccentPrimary), 2f);
        }
        else if (isHovered)
        {
             drawList.AddRectFilled(startPos, startPos + new Vector2(width, height),
                ImGui.GetColorU32(Theme.BgCardHover), 4f);
        }

        // Icon
        float iconSize = 16f;
        var iconPos = startPos + new Vector2(10, (height - iconSize) * 0.5f);
        
        ImGui.PushFont(UiBuilder.IconFont);
        string iconStr = icon.ToIconString();
        var iconColor = isSelected ? Theme.AccentPrimary : (isHovered ? Theme.TextPrimary : Theme.TextSecondary);
        drawList.AddText(UiBuilder.IconFont, 16f, iconPos, ImGui.GetColorU32(iconColor), iconStr);
        ImGui.PopFont();

        // Label
        var textPos = startPos + new Vector2(34, (height - ImGui.GetTextLineHeight()) * 0.5f);
        if (subtext != null) 
            textPos = startPos + new Vector2(34, 4); // Shift up if subtext exists

        var textColor = isSelected ? Theme.TextPrimary : (isHovered ? Theme.TextPrimary : Theme.TextSecondary);
        drawList.AddText(textPos, ImGui.GetColorU32(textColor), label);

        // Subtext (World Name)
        if (subtext != null)
        {
             var subPos = startPos + new Vector2(34, 18);
             drawList.AddText(null, 0.9f * ImGui.GetFontSize(), subPos, ImGui.GetColorU32(Theme.TextDisabled), subtext);
        }
    }

    private string GetWorldName(uint worldId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        if (sheet != null && sheet.TryGetRow(worldId, out var world))
        {
            return world.Name.ToString();
        }
        return $"#{worldId}";
    }
}

