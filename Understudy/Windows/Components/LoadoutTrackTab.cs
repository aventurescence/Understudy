using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// "Track" tab within the Loadout popup — captures gear snapshots and shows BiS status.
/// </summary>
public class LoadoutTrackTab
{
    private readonly Plugin plugin;
    private readonly LoadoutPopupShared shared;

    public LoadoutTrackTab(Plugin plugin, LoadoutPopupShared shared)
    {
        this.plugin = plugin;
        this.shared = shared;
    }

    public void Draw()
    {
        ImGui.Spacing();
        shared.DrawSectionHeader("1. Gear Snapshot", "Capture your currently equipped gear for tracking.");

        ImGui.Indent(12);

        bool isCurrentCharacter = shared.CurrentCharacterId.HasValue
            && shared.CurrentCharacterId.Value == plugin.CharacterTracker.CurrentContentId;

        bool isCurrentJob = isCurrentCharacter
            && Plugin.ObjectTable.Length > 0
            && Plugin.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc
            && pc.ClassJob.RowId == shared.ManualJobId;

        if (isCurrentJob)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentSuccess);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentSuccess with { W = 0.85f });
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentSuccess with { W = 0.70f });
            if (ImGui.Button("Snapshot Current Gear", new Vector2(260, 36)))
            {
                plugin.CharacterTracker.TrackCurrentGearset(shared.CurrentCharacterId);
            }
            ImGui.PopStyleColor(3);
        }
        else if (!isCurrentCharacter)
        {
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X - 24;
            dl.AddRectFilled(pos, pos + new Vector2(width, 30), ImGui.GetColorU32(Theme.AccentWarning with { W = 0.08f }), 4f);
            dl.AddRectFilled(pos, pos + new Vector2(3, 30), ImGui.GetColorU32(Theme.AccentWarning), 2f);
            dl.AddText(pos + new Vector2(12, 7), ImGui.GetColorU32(Theme.AccentWarning), "Log in as this character to snapshot gear.");
            ImGui.Dummy(new Vector2(0, 34));
        }
        else
        {
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X - 24;
            dl.AddRectFilled(pos, pos + new Vector2(width, 30), ImGui.GetColorU32(Theme.AccentWarning with { W = 0.08f }), 4f);
            dl.AddRectFilled(pos, pos + new Vector2(3, 30), ImGui.GetColorU32(Theme.AccentWarning), 2f);
            dl.AddText(pos + new Vector2(12, 7), ImGui.GetColorU32(Theme.AccentWarning), "Switch to this job in-game to snapshot gear.");
            ImGui.Dummy(new Vector2(0, 34));
        }

        ImGui.Unindent(12);

        ImGui.Spacing();
        shared.DrawAccentSeparator();
        ImGui.Spacing();

        shared.DrawSectionHeader("2. BiS Target", "Set a Best-in-Slot loadout to compare against (optional).");

        ImGui.Indent(12);
        ImGui.TextColored(Theme.TextDisabled, "Use the Browse Etro.gg or Import URL tabs to set a BiS target.");

        ImGui.Spacing();

        DrawBiSStatusCard();

        ImGui.Unindent(12);
    }

    private void DrawBiSStatusCard()
    {
        var sdl = ImGui.GetWindowDrawList();
        var statusPos = ImGui.GetCursorScreenPos();
        var statusWidth = ImGui.GetContentRegionAvail().X - 24;

        if (shared.CurrentCharacterId.HasValue
            && plugin.Configuration.Characters.TryGetValue(shared.CurrentCharacterId.Value, out var charData)
            && charData.BisSets.TryGetValue(shared.ManualJobId, out var bis))
        {
            var cardHeight = 32f;
            sdl.AddRectFilled(statusPos, statusPos + new Vector2(statusWidth, cardHeight), ImGui.GetColorU32(Theme.AccentSuccess with { W = 0.08f }), Theme.RowRounding);
            sdl.AddRect(statusPos, statusPos + new Vector2(statusWidth, cardHeight), ImGui.GetColorU32(Theme.AccentSuccess with { W = 0.25f }), Theme.RowRounding);
            sdl.AddRectFilled(statusPos, statusPos + new Vector2(3, cardHeight), ImGui.GetColorU32(Theme.AccentSuccess), 2f);

            var pillText = bis.SourceType;
            var pillTextSize = ImGui.CalcTextSize(pillText);
            var pillPadding = new Vector2(8, 3);
            var pillTotalWidth = pillTextSize.X + pillPadding.X * 2;
            var pillPos = statusPos + new Vector2(statusWidth - pillTotalWidth - 14, (cardHeight - pillTextSize.Y - pillPadding.Y * 2) * 0.5f);
            shared.DrawPill(sdl, pillPos, pillText, Theme.AccentSecondary);

            var bisName = bis.Name;
            var maxNameWidth = statusWidth - pillTotalWidth - 42;
            var nameSize = ImGui.CalcTextSize(bisName);
            if (nameSize.X > maxNameWidth)
            {
                while (bisName.Length > 3 && ImGui.CalcTextSize(bisName + "...").X > maxNameWidth)
                    bisName = bisName[..^1];
                bisName += "...";
            }
            var nameY = (cardHeight - ImGui.GetTextLineHeight()) * 0.5f;
            sdl.AddText(statusPos + new Vector2(14, nameY), ImGui.GetColorU32(Theme.AccentSuccess), bisName);

            ImGui.Dummy(new Vector2(0, cardHeight + 4));

            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentDanger with { W = 0.15f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentDanger with { W = 0.30f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentDanger);
            if (ImGui.Button("Clear BiS Target", new Vector2(160, 28)))
            {
                charData.BisSets.Remove(shared.ManualJobId);
                plugin.Configuration.Save();
            }
            ImGui.PopStyleColor(3);
        }
        else
        {
            sdl.AddRectFilled(statusPos, statusPos + new Vector2(statusWidth, 36), ImGui.GetColorU32(Theme.BgDark), Theme.RowRounding);
            sdl.AddRect(statusPos, statusPos + new Vector2(statusWidth, 36), ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), Theme.RowRounding, ImDrawFlags.None, 1f);
            sdl.AddText(statusPos + new Vector2(14, 10), ImGui.GetColorU32(Theme.TextDisabled), "No BiS target set — tracking gear only");
            ImGui.Dummy(new Vector2(0, 40));
        }
    }
}
