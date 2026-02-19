using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

public class CharacterDetail
{
    private readonly Plugin plugin;
    private readonly Action onBackRequested;
    private readonly LoadoutPopup loadoutPopup;
    private readonly LoadoutCard loadoutCard;

    private ulong? characterId;

    public CharacterDetail(Plugin plugin, Action onBackRequested, LoadoutPopup loadoutPopup)
    {
        this.plugin = plugin;
        this.onBackRequested = onBackRequested;
        this.loadoutPopup = loadoutPopup;

        var materiaDisplay = new MateriaDisplay(plugin);
        var bisView = new BiSComparisonView(plugin, materiaDisplay);
        loadoutCard = new LoadoutCard(plugin, bisView, materiaDisplay);
    }

    public void SetCharacter(ulong id)
    {
        characterId = id;
    }

    public void Draw()
    {
        if (characterId == null || !plugin.Configuration.Characters.TryGetValue(characterId.Value, out var data))
            return;

        // Header with "back" button and character data
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgCard);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);
        if (ImGui.Button("<< Back")) onBackRequested();
        ImGui.PopStyleColor(2);

        ImGui.SameLine();

        // Large name with accent
        ImGui.SetWindowFontScale(1.5f);
        ImGui.TextColored(Theme.AccentPrimary, data.Name);
        ImGui.SetWindowFontScale(1.0f);

        ImGui.SameLine();

        // World badge (simulated pill shape)
        var worldName = SharedDrawHelpers.GetWorldName(data.WorldId);
        var badgeText = $" {worldName} ";
        var badgeSize = ImGui.CalcTextSize(badgeText);
        var badgePos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(
            badgePos - new Vector2(0, 2),
            badgePos + badgeSize + new Vector2(0, 4),
            ImGui.GetColorU32(Theme.BgCardHover),
            10.0f);
        ImGui.TextColored(Theme.TextSecondary, badgeText);

        // Decorative line
        ImGui.Spacing();
        var linePos = ImGui.GetCursorScreenPos();
        dl.AddLine(
            linePos,
            linePos + new Vector2(ImGui.GetContentRegionAvail().X, 0),
            ImGui.GetColorU32(Theme.BorderSubtle),
            1.0f);
        ImGui.Spacing();
        ImGui.Spacing();

        // ── Tomestones & Raid Miscellany Card ─────────────────────
        DrawCard("TOMESTONES & RAID MISCELLANY", () =>
        {
            DrawTomestoneRow("Mnemonics", data.Tomestones.Mnemonics, data.Tomestones.MnemonicsCap,
                data.Tomestones.MnemonicsWeekly, data.Tomestones.MnemonicsWeeklyCap,
                Theme.AccentDanger);

            ImGui.Spacing();

            DrawTomestoneRow("Mathematics", data.Tomestones.Mathematics, data.Tomestones.MathematicsCap,
                0, 0,
                Theme.TextSecondary);

            ImGui.Spacing();
            ImGui.Spacing();

            DrawMiscellanySection(data.Miscellany);
        });

        // ── Raid Progress Card ──────────────────────────────────
        DrawCard("RAID PROGRESS — WEEKLY", () =>
        {
            var width = ImGui.GetContentRegionAvail().X - Theme.CardPadding * 2;
            using var table = ImRaii.Table("RaidGrid", 4, ImGuiTableFlags.BordersInnerV, new Vector2(width, 0));
            if (!table) return;

            ImGui.TableSetupColumn("M1");
            ImGui.TableSetupColumn("M2");
            ImGui.TableSetupColumn("M3");
            ImGui.TableSetupColumn("M4");

            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.TableHeadersRow();
            ImGui.PopStyleColor();

            ImGui.TableNextRow();
            var raidImageIds = plugin.RaidManager.GetAllRaidImageIds();
            SharedDrawHelpers.DrawRaidBox("M1", data.RaidProgress.M1, raidImageIds?.GetValueOrDefault("M1") ?? 0);
            SharedDrawHelpers.DrawRaidBox("M2", data.RaidProgress.M2, raidImageIds?.GetValueOrDefault("M2") ?? 0);
            SharedDrawHelpers.DrawRaidBox("M3", data.RaidProgress.M3, raidImageIds?.GetValueOrDefault("M3") ?? 0);
            SharedDrawHelpers.DrawRaidBox("M4", data.RaidProgress.M4, raidImageIds?.GetValueOrDefault("M4") ?? 0);
        });

        // ── Loadouts Card (Combined Gearsets + BiS) ────────────────
        DrawCard("LOADOUTS", () =>
        {
            var allJobIds = new HashSet<uint>(data.GearSets.Keys);
            foreach (var jobId in data.BisSets.Keys)
                allJobIds.Add(jobId);

            if (allJobIds.Count == 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(Theme.TextDisabled, "  No gearsets tracked yet.");
                ImGui.TextColored(Theme.TextDisabled, "  Click  + Track Gearset  to snapshot your equipped gear,");
                ImGui.TextColored(Theme.TextDisabled, "  or  + Import BiS  to load a target set.");
                ImGui.Spacing();
            }

            uint? jobToRemove = null;
            uint? bisToRemove = null;

            var sortedJobs = allJobIds
                .OrderByDescending(id => data.GearSets.TryGetValue(id, out var gs) ? gs.AverageItemLevel : 0f)
                .ToList();

            foreach (var jobId in sortedJobs)
            {
                data.GearSets.TryGetValue(jobId, out var gearSet);
                data.BisSets.TryGetValue(jobId, out var bisData);
                loadoutCard.Draw(jobId, gearSet, bisData, ref jobToRemove, ref bisToRemove);
            }

            if (jobToRemove.HasValue)
            {
                data.GearSets.Remove(jobToRemove.Value);
                if (data.BisSets.ContainsKey(jobToRemove.Value))
                    data.BisSets.Remove(jobToRemove.Value);
                plugin.Configuration.Save();
            }

            if (bisToRemove.HasValue)
            {
                data.BisSets.Remove(bisToRemove.Value);
                plugin.Configuration.Save();
            }
        },
        headerAction: () =>
        {
            if (characterId.HasValue)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgCard);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);

                if (ImGui.Button("+ Add Loadout"))
                {
                     loadoutPopup.Open(characterId.Value);
                }
                ImGui.PopStyleColor(2);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Track current gear, import BiS from Etro.gg,\nor manually build a loadout.");
            }
        });
    }

    // ── Card container with background, accent border and inner margins ──
    private void DrawCard(string title, Action content, Action? headerAction = null)
    {
        var drawList = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X - Theme.CardPadding;

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        ImGui.BeginGroup();

        ImGui.Dummy(new Vector2(0, Theme.CardPadding));
        ImGui.Indent(Theme.CardPadding);

        ImGui.TextColored(Theme.AccentPrimary, title);

        if (headerAction != null)
        {
             ImGui.SameLine();
             var currentX = ImGui.GetCursorPosX();
             ImGui.SetCursorPosX(currentX + 10);
             headerAction();
        }

        var sepPos = ImGui.GetCursorScreenPos();
        drawList.AddLine(
            sepPos,
            sepPos + new Vector2(availWidth - Theme.CardPadding * 2, 0),
            ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.25f }),
            1.0f);
        ImGui.Spacing();

        content();

        ImGui.Unindent(Theme.CardPadding);
        ImGui.Dummy(new Vector2(0, Theme.CardPadding));
        ImGui.EndGroup();

        var endPos = ImGui.GetItemRectMax();
        var pMin = startPos;
        var pMax = new Vector2(startPos.X + availWidth, endPos.Y + 4);

        drawList.ChannelsSetCurrent(0);

        drawList.AddRectFilled(pMin, pMax, ImGui.GetColorU32(Theme.BgCard), Theme.CardRounding);
        drawList.AddRect(pMin, pMax, ImGui.GetColorU32(Theme.BorderCard), Theme.CardRounding);
        drawList.AddLine(
            pMin + new Vector2(Theme.CardRounding, 0),
            new Vector2(pMax.X - Theme.CardRounding, pMin.Y),
            ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.5f }),
            2.0f);

        drawList.ChannelsMerge();

        ImGui.Dummy(new Vector2(0, Theme.SectionSpace));
    }

    private void DrawTomestoneRow(string name, int current, int max, int weekly, int weeklyMax, Vector4 color)
    {
        ImGui.TextColored(Theme.TextPrimary, name);

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.BgDark);
        float fraction = max > 0 ? (float)current / max : 0f;
        var barWidth = ImGui.GetContentRegionAvail().X - Theme.CardPadding * 2;
        ImGui.ProgressBar(fraction > 1f ? 1f : fraction, new Vector2(barWidth, Theme.BarHeight), $"{current} / {max}");
        ImGui.PopStyleColor(2);

        if (weeklyMax > 0)
        {
            var wRatio = (float)weekly / weeklyMax;
            var wColor = weekly >= weeklyMax ? Theme.AccentSuccess : Theme.ProgressColor(wRatio);
            var capText = weekly >= weeklyMax ? "  ✓ Capped" : "";
            ImGui.TextColored(wColor, $"  Weekly: {weekly}/{weeklyMax}{capText}");
        }
    }

    private void DrawMiscellanySection(MiscellanyData misc)
    {
        var miscManager = plugin.MiscellanyManager;
        if (miscManager == null) return;

        const float iconSize = 40f;
        const float spacing = 8f;

        ImGui.TextColored(Theme.AccentSecondary, "Savage Books");

        for (int i = 1; i <= 4; i++)
        {
            var key = $"M{i}";
            var count = misc.BookCounts.GetValueOrDefault(key);
            var iconId = miscManager.GetBookIconId(key);

            if (i > 1) ImGui.SameLine(0, spacing);
            SharedDrawHelpers.DrawIconWithCount(iconId, iconSize, count, key);
        }

        ImGui.Spacing();

        ImGui.TextColored(Theme.AccentSecondary, "Upgrade Materials");

        SharedDrawHelpers.DrawIconWithCount(miscManager.GetTwineIconId(), iconSize, misc.TwineCount, "Twine");
        ImGui.SameLine(0, spacing);
        SharedDrawHelpers.DrawIconWithCount(miscManager.GetGlazeIconId(), iconSize, misc.GlazeCount, "Glaze");
        ImGui.SameLine(0, spacing);
        SharedDrawHelpers.DrawIconWithCount(miscManager.GetSolventIconId(), iconSize, misc.SolventCount, "Solvent");

        if (misc.CofferCounts.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.AccentSecondary, "Coffers");

            bool first = true;
            foreach (var kvp in misc.CofferCounts)
            {
                var iconId = miscManager.GetCofferIconId(kvp.Key);
                var displayName = kvp.Key.Replace(TierConfig.CofferDisplayTrim, "");

                if (!first) ImGui.SameLine(0, spacing);
                SharedDrawHelpers.DrawIconWithCount(iconId, iconSize, kvp.Value, displayName);
                first = false;
            }
        }
    }
}
