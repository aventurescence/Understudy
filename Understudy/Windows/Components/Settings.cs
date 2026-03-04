using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Understudy.Windows.Components;

public class Settings
{
    private readonly Plugin plugin;

    public Settings(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Draw()
    {
        // Add a nice top header area
        var dl = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var headerHeight = 60f * ImGui.GetIO().FontGlobalScale;
        
        dl.AddRectFilled(startPos, startPos + new Vector2(availWidth, headerHeight), ImGui.GetColorU32(Theme.BgCard), Theme.CardRounding);
        dl.AddRect(startPos, startPos + new Vector2(availWidth, headerHeight), ImGui.GetColorU32(Theme.BorderCard), Theme.CardRounding);

        var titlePos = startPos + new Vector2(20, (headerHeight - ImGui.GetFontSize() * 1.5f) * 0.5f);
        ImGui.SetCursorScreenPos(titlePos);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(Theme.AccentPrimary, FontAwesomeIcon.Cog.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine(0, 10f);
        ImGui.SetWindowFontScale(1.2f);
        ImGui.TextColored(Theme.TextPrimary, "Understudy Settings");
        ImGui.SetWindowFontScale(1f);

        ImGui.SetCursorScreenPos(startPos + new Vector2(0, headerHeight + 20f));

        if (ImGui.BeginChild("SettingsContent", new Vector2(0, -1), false, ImGuiWindowFlags.None))
        {
            DrawSection("General", FontAwesomeIcon.SlidersH, DrawGeneralSettings);
            ImGui.Spacing(); ImGui.Spacing(); ImGui.Spacing();
            
            DrawSection("Dashboard Appearance", FontAwesomeIcon.Palette, DrawDashboardSettings);
            ImGui.Spacing(); ImGui.Spacing(); ImGui.Spacing();
            
            DrawSection("Character Appearance", FontAwesomeIcon.UserCog, DrawCharacterAppearanceSettings);
            ImGui.Spacing(); ImGui.Spacing(); ImGui.Spacing();
            
            DrawSection("Debug", FontAwesomeIcon.Bug, DrawDebugSettings);
        }
        ImGui.EndChild();
    }

    private void DrawSection(string title, FontAwesomeIcon icon, Action drawContent)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(Theme.AccentPrimary, icon.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine(0, 10f);
        ImGui.TextColored(Theme.TextPrimary, title);
        
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        dl.AddLine(p, p + new Vector2(ImGui.GetContentRegionAvail().X, 0), ImGui.GetColorU32(Theme.BorderSubtle), 1f);
        
        ImGui.Spacing();
        ImGui.Spacing();
        
        ImGui.Indent(30f);
        drawContent();
        ImGui.Unindent(30f);
    }

    private void DrawGeneralSettings()
    {
        bool showJobCategory = plugin.Configuration.ShowJobCategoryInDashboard;
        if (ImGui.Checkbox("Show Job Category in Dashboard", ref showJobCategory))
        {
            plugin.Configuration.ShowJobCategoryInDashboard = showJobCategory;
            plugin.Configuration.Save();
        }
        ImGui.TextColored(Theme.TextSecondary, "Displays the highest item level job abbreviation on character cards in the dashboard.");
        
        ImGuiHelpers.ScaledDummy(10f);

        bool compactMode = plugin.Configuration.CompactMode;
        if (ImGui.Checkbox("Compact Mode", ref compactMode))
        {
            plugin.Configuration.CompactMode = compactMode;
            plugin.Configuration.Save();
        }
        ImGui.TextColored(Theme.TextSecondary, "Reduces the vertical height of character cards for a denser layout.");

        ImGuiHelpers.ScaledDummy(10f);

        bool reorderUnlocked = plugin.Configuration.ReorderUnlocked;
        if (ImGui.Checkbox("Unlock Character Reordering", ref reorderUnlocked))
        {
            plugin.Configuration.ReorderUnlocked = reorderUnlocked;
            plugin.Configuration.Save();
        }
        ImGui.TextColored(Theme.TextSecondary, "Enables drag-and-drop character reordering in the dashboard and sidebar.");

        ImGuiHelpers.ScaledDummy(10f);

        if (ImGui.Button("View Changelog"))
        {
            plugin.ChangelogWindow.IsOpen = true;
        }
        ImGui.TextColored(Theme.TextSecondary, "Re-open the latest update notes.");
    }

    private void DrawDashboardSettings()
    {
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

        ImGui.SetNextItemWidth(300f * ImGui.GetIO().FontGlobalScale);
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
        ImGui.TextColored(Theme.TextSecondary, "Select a global Adventurer Plate frame to overlay on character cards.");

        ImGuiHelpers.ScaledDummy(10f);

        float opacity = plugin.Configuration.DashboardFrameOpacity;
        ImGui.SetNextItemWidth(300f * ImGui.GetIO().FontGlobalScale);
        if (ImGui.SliderFloat("Frame Overlay Opacity", ref opacity, 0f, 1f))
        {
            plugin.Configuration.DashboardFrameOpacity = opacity;
            plugin.Configuration.Save();
        }
    }

    private void DrawCharacterAppearanceSettings()
    {
        var order = plugin.Configuration.CharacterOrder;
        var characters = plugin.Configuration.Characters;
        
        if (characters.Count == 0)
        {
            ImGui.TextColored(Theme.TextDisabled, "No characters tracked yet.");
            return;
        }

        var charaCardDecorationSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.CharaCardDecoration>();

        // We use a table to neatly align the settings for each character
        if (ImGui.BeginTable("CharacterAppearanceTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.PadOuterX))
        {
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 200f * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("Frame", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Opacity", ImGuiTableColumnFlags.WidthFixed, 200f * ImGui.GetIO().FontGlobalScale);
            ImGui.TableHeadersRow();

            foreach (var id in order)
            {
                if (!characters.TryGetValue(id, out var data)) continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Theme.AccentSecondary, data.Name);

                ImGui.TableNextColumn();
                uint currentFrame = data.FrameImageId;
                string previewLabel = currentFrame == 0 ? "Use Global Default" : $"# {currentFrame}";

                if (currentFrame != 0 && charaCardDecorationSheet != null)
                {
                    foreach (var dec in charaCardDecorationSheet)
                    {
                        if ((uint)dec.Image == currentFrame)
                        {
                            previewLabel = dec.Name.ToString();
                            break;
                        }
                    }
                }

                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo($"##Frame_{id}", previewLabel))
                {
                    if (ImGui.Selectable($"Use Global Default##{id}", currentFrame == 0))
                    {
                        data.FrameImageId = 0;
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

                            if (ImGui.Selectable($"{name}##{id}_{imageId}", currentFrame == imageId))
                            {
                                data.FrameImageId = imageId;
                                plugin.Configuration.Save();
                            }
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.TableNextColumn();
                if (data.FrameImageId != 0)
                {
                    float charOpacity = data.FrameOpacity;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.SliderFloat($"##Opacity_{id}", ref charOpacity, 0f, 1f))
                    {
                        data.FrameOpacity = charOpacity;
                        plugin.Configuration.Save();
                    }
                }
                else
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextColored(Theme.TextDisabled, "Using Global");
                }
            }
            ImGui.EndTable();
        }
    }

    private void DrawDebugSettings()
    {
        bool verbose = plugin.Configuration.VerboseLogging;
        if (ImGui.Checkbox("Verbose Logging", ref verbose))
        {
            plugin.Configuration.VerboseLogging = verbose;
            plugin.Configuration.Save();
        }
        ImGui.TextColored(Theme.TextSecondary, "Outputs extra diagnostic information to the Dalamud log. Leave disabled for normal use.");
    }

    private static bool IsValidFrameId(uint id) =>
        (id >= 198001 && id <= 198022) ||
        (id >= 198654 && id <= 198673) ||
        (id >= 198701 && id <= 198726) ||
        id == 198901 || id == 198902;
}
