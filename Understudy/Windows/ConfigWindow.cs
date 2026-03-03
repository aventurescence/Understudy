using System;
using System.Numerics;

using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Understudy;

namespace Understudy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Understudy — Settings###UnderstudyConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Theme.BgDark);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.BorderSubtle);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Theme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.BgCard);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Theme.BgCardHover);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);

        if (configuration.IsConfigWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(6);
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        ImGui.TextColored(Theme.TextSecondary, "General Settings");
        ImGui.Separator();

        var compactMode = configuration.CompactMode;
        if (ImGui.Checkbox("Compact Mode", ref compactMode))
        {
            configuration.CompactMode = compactMode;
            configuration.Save();
        }

        var showJobCategory = configuration.ShowJobCategoryInDashboard;
        if (ImGui.Checkbox("Show Job Category in Dashboard", ref showJobCategory))
        {
            configuration.ShowJobCategoryInDashboard = showJobCategory;
            configuration.Save();
        }

        var showInDuty = configuration.ShowInDuty;
        if (ImGui.Checkbox("Show Plugin in Duty", ref showInDuty))
        {
            configuration.ShowInDuty = showInDuty;
            configuration.Save();
        }

        var reorderUnlocked = configuration.ReorderUnlocked;
        if (ImGui.Checkbox("Unlock Character Reordering", ref reorderUnlocked))
        {
            configuration.ReorderUnlocked = reorderUnlocked;
            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0, 5));
        ImGui.TextColored(Theme.TextSecondary, "UI & Layout");
        ImGui.Separator();

        var dashboardFrameOpacity = configuration.DashboardFrameOpacity;
        ImGui.SetNextItemWidth(150f);
        if (ImGui.SliderFloat("Dashboard Background Opacity", ref dashboardFrameOpacity, 0.0f, 1.0f, "%.2f"))
        {
            configuration.DashboardFrameOpacity = dashboardFrameOpacity;
            configuration.Save();
        }

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0, 5));
        ImGui.TextColored(Theme.TextSecondary, "Advanced");
        ImGui.Separator();

        var verboseLogging = configuration.VerboseLogging;
        if (ImGui.Checkbox("Verbose Logging", ref verboseLogging))
        {
            configuration.VerboseLogging = verboseLogging;
            configuration.Save();
        }

        ImGui.PopStyleVar();
    }
}
