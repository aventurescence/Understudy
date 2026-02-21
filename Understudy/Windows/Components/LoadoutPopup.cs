using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace Understudy.Windows.Components;

/// <summary>
/// Modal popup for managing a character's loadout — orchestrates tab components.
/// </summary>
public class LoadoutPopup : IDisposable
{
    private readonly Plugin plugin;
    private readonly LoadoutPopupShared shared = new();

    private readonly LoadoutTrackTab trackTab;
    private readonly LoadoutEtroBrowseTab etroBrowseTab;
    private readonly LoadoutManualBuilderTab manualBuilderTab;
    private readonly LoadoutUrlImportTab urlImportTab;

    private bool isOpen = false;
    private bool shouldOpen = false;
    private int activeTab = 0;

    public LoadoutPopup(Plugin plugin)
    {
        this.plugin = plugin;

        Action closePopup = () =>
        {
            isOpen = false;
            ImGui.CloseCurrentPopup();
        };

        trackTab = new LoadoutTrackTab(plugin, shared);
        etroBrowseTab = new LoadoutEtroBrowseTab(plugin, shared, closePopup);
        manualBuilderTab = new LoadoutManualBuilderTab(plugin, shared);
        urlImportTab = new LoadoutUrlImportTab(plugin, shared, closePopup);
    }

    public void Open(ulong characterId)
    {
        shared.CurrentCharacterId = characterId;
        isOpen = true;

        if (Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc)
        {
            shared.ManualJobId = plugin.IsJobExcluded(pc.ClassJob.RowId) ? 0u : pc.ClassJob.RowId;
        }
        else
        {
            shared.ManualJobId = 0;
        }

        etroBrowseTab.RefreshEtroList();
        shouldOpen = true;
    }

    public void Draw()
    {
        if (shouldOpen)
        {
            ImGui.OpenPopup("Manage Loadout");
            shouldOpen = false;
        }

        if (!isOpen) return;

        shared.LoadingFrame++;

        // ── Popup styling ──
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(24, 20));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, Theme.PopupRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Theme.BgPopup);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Theme.BgDark);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Theme.BgDark);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.AccentPrimary with { W = 0.5f });
        ImGui.PushStyleColor(ImGuiCol.Tab, Theme.BgCard);
        ImGui.PushStyleColor(ImGuiCol.TabActive, Theme.TabActiveBg);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, Theme.AccentPrimary with { W = 0.35f });
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Theme.InputFieldBg with { W = 0.85f });

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(720, 0), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Vector2(720, 200), new Vector2(720, ImGui.GetMainViewport().Size.Y * 0.85f));

        if (ImGui.BeginPopupModal("Manage Loadout", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var dl = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();

            dl.AddRectFilled(
                windowPos + new Vector2(1, 1),
                windowPos + new Vector2(windowSize.X - 1, 4),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.6f }));

            DrawJobSelector("Loadout Job");

            ImGui.Spacing();
            shared.DrawAccentSeparator();
            ImGui.Spacing();

            DrawCustomTabBar();

            ImGui.Spacing();

            switch (activeTab)
            {
                case 0: trackTab.Draw(); break;
                case 1: etroBrowseTab.Draw(); break;
                case 2: manualBuilderTab.Draw(); break;
                case 3: urlImportTab.Draw(); break;
            }

            ImGui.Spacing();
            shared.DrawAccentSeparator();
            ImGui.Spacing();

            var footerWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + footerWidth - 110);

            ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgCard);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.BorderSubtle);
            if (ImGui.Button("Close", new Vector2(100, 32)))
            {
                isOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor(3);

            ImGui.EndPopup();
        }
        else
        {
            isOpen = false;
        }

        ImGui.PopStyleColor(9);
        ImGui.PopStyleVar(4);
    }

    // ── Custom tab bar with icon font rendering ──
    private void DrawCustomTabBar()
    {
        var tabs = new (FontAwesomeIcon icon, string label)[]
        {
            (FontAwesomeIcon.Cog, "Track"),
            (FontAwesomeIcon.Globe, "Browse Etro.gg"),
            (FontAwesomeIcon.Wrench, "Manual Builder"),
            (FontAwesomeIcon.Link, "Import URL"),
        };

        var dl = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var totalWidth = ImGui.GetContentRegionAvail().X;
        float tabHeight = 32f;
        float x = startPos.X;

        for (int i = 0; i < tabs.Length; i++)
        {
            var (icon, label) = tabs[i];
            bool isActive = activeTab == i;

            ImGui.PushFont(UiBuilder.IconFont);
            var iconStr = icon.ToIconString();
            var iconSize = ImGui.CalcTextSize(iconStr);
            ImGui.PopFont();
            var labelSize = ImGui.CalcTextSize(label);
            float tabWidth = iconSize.X + 6 + labelSize.X + 24;

            var tabMin = new Vector2(x, startPos.Y);
            var tabMax = new Vector2(x + tabWidth, startPos.Y + tabHeight);

            ImGui.SetCursorScreenPos(tabMin);
            if (i > 0) ImGui.SameLine(0, 0);
            ImGui.InvisibleButton($"##tab_{i}", tabMax - tabMin);
            bool hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) activeTab = i;

            if (isActive)
            {
                dl.AddRectFilled(tabMin, tabMax, ImGui.GetColorU32(Theme.TabActiveBg), 4f, ImDrawFlags.RoundCornersTop);
                dl.AddRectFilled(
                    new Vector2(tabMin.X + 4, tabMax.Y - 2),
                    new Vector2(tabMax.X - 4, tabMax.Y),
                    ImGui.GetColorU32(Theme.AccentPrimary), 1f);
            }
            else if (hovered)
            {
                dl.AddRectFilled(tabMin, tabMax, ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.08f }), 4f, ImDrawFlags.RoundCornersTop);
            }

            var textColor = isActive ? Theme.AccentPrimary : (hovered ? Theme.TextPrimary : Theme.TextSecondary);
            var iconPos = new Vector2(tabMin.X + 12, tabMin.Y + (tabHeight - iconSize.Y) * 0.5f);
            dl.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), iconPos, ImGui.GetColorU32(textColor), iconStr);

            var labelPos = new Vector2(iconPos.X + iconSize.X + 6, tabMin.Y + (tabHeight - labelSize.Y) * 0.5f);
            dl.AddText(labelPos, ImGui.GetColorU32(textColor), label);

            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            x += tabWidth + 4;
        }

        dl.AddLine(
            new Vector2(startPos.X, startPos.Y + tabHeight),
            new Vector2(startPos.X + totalWidth, startPos.Y + tabHeight),
            ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.4f }), 1f);

        ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + tabHeight + 2));
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s[1..];
    }

    private void DrawJobSelector(string label)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        var currentJob = sheet?.GetRow(shared.ManualJobId);
        var preview = currentJob != null ? ToTitleCase(currentJob.Value.Name.ToString()) : "Select Job...";

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.TextSecondary, label);
        ImGui.SameLine();

        if (shared.ManualJobId > 0)
        {
            uint jobIconId = 62100u + shared.ManualJobId;
            var tex = Plugin.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(jobIconId));
            if (tex.TryGetWrap(out var wrap, out _))
            {
                ImGui.Image(wrap.Handle, new Vector2(24, 24));
                ImGui.SameLine(0, 6);
            }
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo($"##{label}Selector", preview))
        {
            if (sheet != null)
            {
                foreach (var job in sheet)
                {
                    if (job.RowId == 0 || job.Abbreviation.ExtractText().IsNullOrEmpty()) continue;
                    if (plugin.IsJobExcluded(job.RowId)) continue;

                    bool isSelected = shared.ManualJobId == job.RowId;
                    if (ImGui.Selectable(ToTitleCase(job.Name.ToString()), isSelected))
                    {
                        shared.ManualJobId = job.RowId;
                        etroBrowseTab.RefreshEtroList();
                        manualBuilderTab.RebuildItemList();
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    public void Dispose()
    {
        etroBrowseTab.Dispose();
        manualBuilderTab.Dispose();
    }
}
