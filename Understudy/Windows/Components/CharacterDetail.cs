using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;
using Understudy.Models;

namespace Understudy.Windows.Components;

public class CharacterDetail
{
    private readonly Plugin plugin;
    private readonly Action onBackRequested;
    private readonly LoadoutPopup loadoutPopup;
    private readonly LoadoutCard loadoutCard;

    private ulong? characterId;

    private uint? loadoutDragSourceJobId;

    public CharacterDetail(Plugin plugin, Action onBackRequested, LoadoutPopup loadoutPopup)
    {
        this.plugin = plugin;
        this.onBackRequested = onBackRequested;
        this.loadoutPopup = loadoutPopup;

        var materiaDisplay = new MateriaDisplay(plugin);
        var bisView = new BiSComparison(plugin, materiaDisplay);
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

        ImGui.Indent(12f);

        ImGui.Spacing();

        var dl = ImGui.GetWindowDrawList();
        var contentWidth = ImGui.GetContentRegionAvail().X;

        ImGui.SetWindowFontScale(1.5f);
        var nameSize = ImGui.CalcTextSize(data.Name);
        ImGui.SetWindowFontScale(1.0f);

        var worldName = SharedDrawHelpers.GetWorldName(data.WorldId);
        var worldText = $" {worldName} ";
        var worldSize = ImGui.CalcTextSize(worldText);
        var worldPillPadding = new Vector2(8, 3);

        float totalWidth = nameSize.X + 12 + worldSize.X + worldPillPadding.X * 2;
        float startX = ImGui.GetCursorPosX() + (contentWidth - totalWidth) * 0.5f;

        ImGui.SetCursorPosX(startX);
        ImGui.SetWindowFontScale(1.5f);
        ImGui.TextColored(Theme.AccentPrimary, data.Name);
        ImGui.SetWindowFontScale(1.0f);

        ImGui.SameLine(0, 12);

        var pillPos = ImGui.GetCursorScreenPos();
        var pillMin = pillPos - new Vector2(0, 2);
        var pillMax = pillPos + worldSize + new Vector2(worldPillPadding.X * 2, 4);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(Theme.BgCardHover), 10f);
        dl.AddRect(pillMin, pillMax, ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.4f }), 10f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + worldPillPadding.X);
        ImGui.TextColored(Theme.TextSecondary, worldText);

        ImGui.Spacing();
        var linePos = ImGui.GetCursorScreenPos();
        dl.AddLine(
            linePos,
            linePos + new Vector2(contentWidth, 0),
            ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.3f }),
            1.5f);
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
            uint? dragSourceJob = loadoutDragSourceJobId;
            uint? dragTargetJob = null;

            List<uint> sortedJobs;
            if (data.LoadoutOrder.Count > 0)
            {
                sortedJobs = new List<uint>(data.LoadoutOrder.Where(id => allJobIds.Contains(id)));
                foreach (var id in allJobIds)
                {
                    if (!sortedJobs.Contains(id))
                        sortedJobs.Add(id);
                }
            }
            else
            {
                sortedJobs = allJobIds
                    .OrderByDescending(id => data.GearSets.TryGetValue(id, out var gs) ? gs.AverageItemLevel : 0f)
                    .ToList();
            }

            foreach (var jobId in sortedJobs)
            {
                // Skip the dragged loadout so remaining cards reflow
                if (loadoutDragSourceJobId.HasValue && loadoutDragSourceJobId.Value == jobId) continue;

                data.GearSets.TryGetValue(jobId, out var gearSet);
                data.BisSets.TryGetValue(jobId, out var bisData);
                loadoutCard.Draw(jobId, gearSet, bisData, ref jobToRemove, ref bisToRemove, ref dragSourceJob, ref dragTargetJob, characterId);
            }

            if (dragSourceJob.HasValue)
                loadoutDragSourceJobId = dragSourceJob;

            if (dragTargetJob.HasValue && loadoutDragSourceJobId.HasValue && dragTargetJob != loadoutDragSourceJobId)
            {
                // Populate LoadoutOrder from current visual order if not already manual
                if (data.LoadoutOrder.Count == 0)
                    data.LoadoutOrder = new List<uint>(sortedJobs);

                var srcIdx = data.LoadoutOrder.IndexOf(loadoutDragSourceJobId.Value);
                var dstIdx = data.LoadoutOrder.IndexOf(dragTargetJob.Value);
                if (srcIdx >= 0 && dstIdx >= 0)
                {
                    data.LoadoutOrder.RemoveAt(srcIdx);
                    var insertIdx = dstIdx > srcIdx ? dstIdx - 1 : dstIdx;
                    data.LoadoutOrder.Insert(insertIdx, loadoutDragSourceJobId.Value);
                    plugin.Configuration.Save();
                }
                loadoutDragSourceJobId = null;
            }

            if (loadoutDragSourceJobId.HasValue && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                loadoutDragSourceJobId = null;

            if (jobToRemove.HasValue)
            {
                data.GearSets.Remove(jobToRemove.Value);
                if (data.BisSets.ContainsKey(jobToRemove.Value))
                    data.BisSets.Remove(jobToRemove.Value);
                data.LoadoutOrder.Remove(jobToRemove.Value);
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

                ImGui.PushFont(UiBuilder.IconFont);
                bool addClicked = ImGui.Button(FontAwesomeIcon.Plus.ToIconString() + "##addloadout");
                ImGui.PopFont();
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Add Loadout");
                if (ImGui.IsItemHovered() || ImGui.IsItemClicked())
                {
                    if (ImGui.IsItemClicked() || (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)))
                         loadoutPopup.Open(characterId.Value);
                }
                ImGui.PopStyleColor(2);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Track current gear, import BiS from Etro.gg,\nor manually build a loadout.");

                if (data.LoadoutOrder.Count > 0)
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, Theme.BgCard);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.BgCardHover);
                    ImGui.PushFont(UiBuilder.IconFont);
                    bool sortClicked = ImGui.Button(FontAwesomeIcon.SyncAlt.ToIconString() + "##autosort");
                    ImGui.PopFont();
                    ImGui.PopStyleColor(2);

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Reset to automatic sorting by item level.");

                    if (sortClicked)
                    {
                        data.LoadoutOrder.Clear();
                        plugin.Configuration.Save();
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Reset to automatic sorting by item level.");
                }
            }
        });

        // ── Character Settings Card ──────────────────────────────
        DrawCard("CHARACTER SETTINGS", () =>
        {
            DrawCharacterFrameSettings(data);
        });

        ImGui.Unindent(12f);
    }

    private void DrawCharacterFrameSettings(CharacterData data)
    {
        ImGui.TextColored(Theme.AccentSecondary, "Card Frame Overlay");
        ImGui.Spacing();

        var charaCardDecorationSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.CharaCardDecoration>();

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

        ImGui.SetNextItemWidth(280f);
        if (ImGui.BeginCombo("Frame", previewLabel))
        {
            if (ImGui.Selectable("Use Global Default", currentFrame == 0))
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

                    if (ImGui.Selectable(name, currentFrame == imageId))
                    {
                        data.FrameImageId = imageId;
                        plugin.Configuration.Save();
                    }
                }
            }
            ImGui.EndCombo();
        }

        if (data.FrameImageId != 0)
        {
            float opacity = data.FrameOpacity;
            ImGui.SetNextItemWidth(280f);
            if (ImGui.SliderFloat("Frame Opacity", ref opacity, 0f, 1f))
            {
                data.FrameOpacity = opacity;
                plugin.Configuration.Save();
            }
        }
    }

    private static bool IsValidFrameId(uint id) =>
        (id >= 198001 && id <= 198022) ||
        (id >= 198654 && id <= 198673) ||
        (id >= 198701 && id <= 198726) ||
        id == 198901 || id == 198902;

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
            if (weekly >= weeklyMax)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text("  ");
                ImGui.SameLine(0, 0);
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(Theme.AccentSuccess, FontAwesomeIcon.Check.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine(0, 4);
                ImGui.TextColored(Theme.AccentSuccess, "Capped");
            }
            else
            {
                ImGui.TextColored(Theme.ProgressColor(wRatio), $"  Weekly: {weekly}/{weeklyMax}");
            }
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

        bool firstMat = true;
        void DrawMaterial(ushort iconId, int count, string label)
        {
            if (iconId == 0) return;
            if (!firstMat) ImGui.SameLine(0, spacing);
            SharedDrawHelpers.DrawIconWithCount(iconId, iconSize, count, label);
            firstMat = false;
        }

        DrawMaterial(miscManager.GetTwineIconId(), misc.TwineCount, "Twine");
        DrawMaterial(miscManager.GetGlazeIconId(), misc.GlazeCount, "Glaze");
        DrawMaterial(miscManager.GetSolventIconId(), misc.SolventCount, "Solvent");

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
