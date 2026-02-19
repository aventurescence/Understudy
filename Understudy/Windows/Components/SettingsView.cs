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
            DrawDebugSettings();
        }
        ImGui.EndChild();
    }

    private void DrawGeneralSettings()
    {
        ImGui.TextColored(Theme.AccentPrimary, "General");
        
        // Example settings - likely just placeholders or simple toggles for now
        // Assuming Configuration has some properties, or we add them.
        // For now, we'll just show some UI elements.
        
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
    }

    private void DrawDebugSettings()
    {
        ImGui.TextColored(Theme.AccentPrimary, "Debug");
        
        bool verbose = plugin.Configuration.VerboseLogging;
        if (ImGui.Checkbox("Verbose Logging", ref verbose))
        {
            plugin.Configuration.VerboseLogging = verbose;
            plugin.Configuration.Save();
        }
    }
}
