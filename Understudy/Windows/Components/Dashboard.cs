using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

public class Dashboard
{
    private readonly Plugin plugin;
    private readonly Action<ulong> onCharacterSelected;
    
    public Dashboard(Plugin plugin, Action<ulong> onCharacterSelected)
    {
        this.plugin = plugin;
        this.onCharacterSelected = onCharacterSelected;
    }

    public void Draw()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.TextPrimary, "Dashboard");
        ImGui.Separator();
        ImGui.Spacing();

        // Grid parameters
        var availWidth = ImGui.GetContentRegionAvail().X;
        var cardWidth = 300f * ImGui.GetIO().FontGlobalScale;
        int columns = Math.Max(1, (int)(availWidth / (cardWidth + 10)));
        
        ImGui.Columns(columns, "DashboardGrid", false);

        foreach (var kvp in plugin.Configuration.Characters)
        {
            var id = kvp.Key;
            var data = kvp.Value;
            
            DrawCharacterCard(id, data, cardWidth);
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        
        if (!plugin.Configuration.Characters.Any())
        {
            ImGui.TextColored(Theme.TextSecondary, "No characters tracked. Add a character to get started.");
        }
    }

    private void DrawCharacterCard(ulong id, CharacterData data, float width)
    {
        var height = 140f * ImGui.GetIO().FontGlobalScale;
        
        // Card Background
        var startPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.BgCard);
        
        if (ImGui.BeginChild($"Card_{id}", new Vector2(width, height), true, ImGuiWindowFlags.NoScrollbar))
        {
            // Header: Name & world
            ImGui.TextColored(Theme.AccentPrimary, data.Name);
            ImGui.SameLine();
            var worldName = GetWorldName(data.WorldId);
            var worldWidth = ImGui.CalcTextSize(worldName).X;
            ImGui.SetCursorPosX(width - worldWidth - 16);
            ImGui.TextColored(Theme.TextDisabled, worldName);
            
            ImGui.Separator();
            ImGui.Spacing();
            
            // Content
            // 1. Tomestones
            var tomes = data.Tomestones;
            float ratio = tomes.MnemonicsWeeklyCap > 0 ? (float)tomes.MnemonicsWeekly / tomes.MnemonicsWeeklyCap : 0f;
            ImGui.Text("Weekly Tomestones");
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Theme.ProgressColor(ratio));
            ImGui.ProgressBar(ratio, new Vector2(width - 24, 16), $"{tomes.MnemonicsWeekly} / {tomes.MnemonicsWeeklyCap}");
            ImGui.PopStyleColor();
            
            ImGui.Spacing();
            
            // 2. Raid Progress & IL in a mini-row
            ImGui.BeginGroup();
            ImGui.TextColored(Theme.TextSecondary, "Raid Status");
            DrawMiniRaidStatus(data.RaidProgress);
            ImGui.EndGroup();
            
            ImGui.SameLine();
            var availX = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availX - 60);
            
            ImGui.BeginGroup();
            float maxIl = data.GearSets.Values.Any() ? data.GearSets.Values.Max(g => g.AverageItemLevel) : 0;
            ImGui.TextColored(Theme.TextSecondary, "Max IL");
            ImGui.SetWindowFontScale(1.2f);
            ImGui.TextColored(Theme.AccentSecondary, maxIl > 0 ? $"{maxIl:F0}" : "-");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.EndGroup();
            
            // Interaction overlay (invisible button over the whole card)
            ImGui.SetCursorScreenPos(startPos);
            ImGui.InvisibleButton($"##Btn_{id}", new Vector2(width, height));
            if (ImGui.IsItemHovered())
            {
                drawList.AddRect(startPos, startPos + new Vector2(width, height), ImGui.GetColorU32(Theme.AccentPrimary), 8f, ImDrawFlags.None, 2f);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                onCharacterSelected(id);
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void DrawMiniRaidStatus(RaidData progress)
    {
        var size = new Vector2(24, 24);
        var raidImageIds = plugin.RaidManager.GetAllRaidImageIds();

        DrawMiniIcon(progress.M1, "M1", raidImageIds?.GetValueOrDefault("M1") ?? 0); ImGui.SameLine(0, 4);
        DrawMiniIcon(progress.M2, "M2", raidImageIds?.GetValueOrDefault("M2") ?? 0); ImGui.SameLine(0, 4);
        DrawMiniIcon(progress.M3, "M3", raidImageIds?.GetValueOrDefault("M3") ?? 0); ImGui.SameLine(0, 4);
        DrawMiniIcon(progress.M4, "M4", raidImageIds?.GetValueOrDefault("M4") ?? 0);

        void DrawMiniIcon(bool cleared, string tooltip, uint imageId)
        {
            if (imageId != 0)
            {
                var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(imageId));
                if (tex.TryGetWrap(out var wrap, out _))
                {
                    var tint = cleared ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 0.4f);
                    ImGui.Image(wrap.Handle, size, Vector2.Zero, Vector2.One, tint);

                    // Border with glow if cleared
                    if (cleared)
                    {
                        var min = ImGui.GetItemRectMin();
                        var max = ImGui.GetItemRectMax();
                        var dl = ImGui.GetWindowDrawList();
                        dl.AddRect(min, max, ImGui.GetColorU32(Theme.AccentSuccess), 3f, ImDrawFlags.None, 2.0f);
                        // Subtle glow
                        dl.AddRect(min - new Vector2(1, 1), max + new Vector2(1, 1),
                            ImGui.GetColorU32(Theme.GlowSuccess), 4f);
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(tooltip + (cleared ? ": Cleared" : ": Incomplete"));
                    return;
                }
            }

            // Fallback — flat color square
            var col = cleared ? Theme.AccentSuccess : new Vector4(0.2f, 0.2f, 0.25f, 1f);
            ImGui.ColorButton($"##{tooltip}{progress.GetHashCode()}", col, ImGuiColorEditFlags.NoTooltip, size);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip + (cleared ? ": Cleared" : ": Incomplete"));
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
