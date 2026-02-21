using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Understudy.Windows.Components;

public class SettingsView
{
    private readonly Plugin plugin;

    public SettingsView(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Draw()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.TextPrimary, "Settings");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginChild("SettingsContent", new Vector2(0, -1), false, ImGuiWindowFlags.None))
        {
            DrawGeneralSettings();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawDashboardSettings();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawDebugSettings();
        }
        ImGui.EndChild();
    }

    private void DrawGeneralSettings()
    {
        ImGui.TextColored(Theme.AccentPrimary, "General");
        ImGui.Indent(12f);
        
        bool showJobCategory = plugin.Configuration.ShowJobCategoryInDashboard;
        if (ImGui.Checkbox("Show Job Category in Dashboard", ref showJobCategory))
        {
            plugin.Configuration.ShowJobCategoryInDashboard = showJobCategory;
            plugin.Configuration.Save();
        }
        
        ImGuiHelpers.ScaledDummy(5f);

        bool compactMode = plugin.Configuration.CompactMode;
        if (ImGui.Checkbox("Compact Mode", ref compactMode))
        {
            plugin.Configuration.CompactMode = compactMode;
            plugin.Configuration.Save();
        }
        ImGui.Unindent(12f);
    }



    private void DrawDashboardSettings()
    {
        ImGui.TextColored(Theme.AccentPrimary, "Dashboard Appearance");
        ImGui.Indent(12f);

        var charaCardDecorationSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.CharaCardDecoration>();
        
        uint currentFrame = plugin.Configuration.DashboardFrameImageId;
        string previewLabel = "None";
        if (currentFrame == 0) previewLabel = "None";
        else if (charaCardDecorationSheet != null && IsValidFrameId(currentFrame))
        {
            foreach (var dec in charaCardDecorationSheet)
            {
                if ((uint)dec.Image == currentFrame)
                {
                    previewLabel = dec.Name.ToString();
                    break;
                }
            }
            if (previewLabel == "None") previewLabel = $"# {currentFrame}";
        }

        if (ImGui.BeginCombo("Card Frame Overlay", previewLabel))
        {
            if (ImGui.Selectable("None", currentFrame == 0))
            {
                plugin.Configuration.DashboardFrameImageId = 0;
                plugin.Configuration.Save();
            }

            if (charaCardDecorationSheet != null)
            {
                foreach (var decoration in charaCardDecorationSheet)
                {
                    var name = decoration.Name.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    uint imageId = (uint)decoration.Image;

                    if (!IsValidFrameId(imageId)) continue;

                    if (ImGui.Selectable(name, currentFrame == imageId))
                    {
                        plugin.Configuration.DashboardFrameImageId = imageId;
                        plugin.Configuration.Save();
                    }
                }
            }
            ImGui.EndCombo();
        }

        float opacity = plugin.Configuration.DashboardFrameOpacity;
        if (ImGui.SliderFloat("Frame Overlay Opacity", ref opacity, 0f, 1f))
        {
            plugin.Configuration.DashboardFrameOpacity = opacity;
            plugin.Configuration.Save();
        }
        ImGui.Unindent(12f);
    }

    private void DrawDebugSettings()
    {
        ImGui.TextColored(Theme.AccentPrimary, "Debug");
        ImGui.Indent(12f);
        
        bool verbose = plugin.Configuration.VerboseLogging;
        if (ImGui.Checkbox("Verbose Logging", ref verbose))
        {
            plugin.Configuration.VerboseLogging = verbose;
            plugin.Configuration.Save();
        }
        ImGui.Unindent(12f);
    }

    private static bool IsValidFrameId(uint id) =>
        (id >= 198001 && id <= 198022) ||
        (id >= 198654 && id <= 198673) ||
        (id >= 198701 && id <= 198726) ||
        id == 198901 || id == 198902;
}
