using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// "Import URL" tab within the Loadout popup — paste an Etro.gg or XIVGear.app link.
/// </summary>
public class LoadoutUrlImportTab
{
    private readonly Plugin plugin;
    private readonly LoadoutPopupShared shared;
    private readonly System.Action closePopup;

    private string importUrl = "";

    public LoadoutUrlImportTab(Plugin plugin, LoadoutPopupShared shared, System.Action closePopup)
    {
        this.plugin = plugin;
        this.shared = shared;
        this.closePopup = closePopup;
    }

    public void Draw()
    {
        ImGui.Spacing();

        shared.DrawSectionHeader("Paste a Gearset Link", "Import from Etro.gg or XIVGear.app");

        ImGui.Spacing();
        ImGui.Indent(12);

        var hintDl = ImGui.GetWindowDrawList();
        var hintPos = ImGui.GetCursorScreenPos();
        shared.DrawPill(hintDl, hintPos, "etro.gg", Theme.ProviderEtro);
        var etroWidth = ImGui.CalcTextSize("etro.gg").X + 22;
        shared.DrawPill(hintDl, hintPos + new Vector2(etroWidth + 8, 0), "xivgear.app", Theme.ProviderXIVGear);
        ImGui.Dummy(new Vector2(0, 26));

        ImGui.Spacing();

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 10));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
        ImGui.InputTextWithHint("##importUrl", "https://etro.gg/gearset/...", ref importUrl, 512);

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);

        ImGui.Spacing();
        ImGui.Spacing();

        bool canImport = !string.IsNullOrWhiteSpace(importUrl) && shared.ManualJobId != 0;

        if (!canImport) ImGui.BeginDisabled();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.70f });
        if (ImGui.Button("Import Gearset", new Vector2(180, 38)))
        {
            if (shared.CurrentCharacterId.HasValue && plugin.Configuration.Characters.TryGetValue(shared.CurrentCharacterId.Value, out _))
            {
                plugin.BiSImportManager.ImportFromUrl(importUrl, shared.ManualJobId, shared.CurrentCharacterId);
                closePopup();
            }
        }
        ImGui.PopStyleColor(3);

        if (!canImport) ImGui.EndDisabled();

        if (!canImport && shared.ManualJobId == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.AccentWarning, "Select a job above before importing.");
        }

        ImGui.Unindent(12);
    }
}
