using System;
using System.Numerics;

using Dalamud.Interface.Windowing;
using Understudy;

namespace Understudy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Understudy — Settings###UnderstudyConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(280, 110);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Push the same colors as the main theme
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Theme.BgDark);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.BorderSubtle);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Theme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.BgCard);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Theme.BgCardHover);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);

        // Movable flag
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
        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        var showInDuty = configuration.ShowInDuty;
        if (ImGui.Checkbox("Show in Duty", ref showInDuty))
        {
            configuration.ShowInDuty = showInDuty;
            configuration.Save();
        }
    }
}
