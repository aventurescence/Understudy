using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Understudy.Managers;

namespace Understudy.Windows.Components;

public class LoadoutPopup : IDisposable
{
    private readonly Plugin plugin;
    private bool isOpen = false;
    private ulong? currentCharacterId;

    // Popup state
    private string importUrl = "";
    private bool shouldOpen = false;

    // Manual entry state
    private int manualMinIl = 710;
    private int manualMaxIl = 735;
    private int defaultMinIl = 710;
    private int defaultMaxIl = 735;
    private bool ilRangeInitialized = false;
    private int manualSlotId = 0;
    private uint manualJobId = 0;
    private List<Lumina.Excel.Sheets.Item> manualSearchResults = new();
    private int selectedItemIndex = -1;
    private List<Lumina.Excel.Sheets.Item> foodSearchResults = new();
    private bool foodListBuilt = false;

    // Armory slot icon UVs
    private static readonly Dictionary<int, Vector4> SlotIconUVs = new()
    {
        { 0, new Vector4(0f, 0f, 0.25f, 0.23255815f) },
        { 1, new Vector4(0.75f, 0.25f, 1f, 0.4651163f) },
        { 2, new Vector4(0.25f, 0f, 0.5f, 0.23255815f) },
        { 3, new Vector4(0.5f, 0f, 0.75f, 0.23255815f) },
        { 4, new Vector4(0.75f, 0f, 1f, 0.23255815f) },
        { 6, new Vector4(0.25f, 0.25f, 0.5f, 0.4651163f) },
        { 7, new Vector4(0.5f, 0.25f, 0.75f, 0.4651163f) },
        { 8, new Vector4(0f, 0.5f, 0.25f, 0.6976744f) },
        { 9, new Vector4(0.25f, 0.5f, 0.5f, 0.6976744f) },
        { 10, new Vector4(0.5f, 0.5f, 0.75f, 0.6976744f) },
        { 11, new Vector4(0.75f, 0.5f, 1f, 0.6976744f) },
        { 12, new Vector4(0.75f, 0.5f, 1f, 0.6976744f) },
    };

    // Etro Browse State
    private List<EtroBiSSet> etroSets = new();
    private List<EtroBiSSet> filteredEtroSets = new();
    private bool isLoadingEtro = false;
    private string etroSearch = "";

    // Tab state
    private int activeTab = 0;

    // Animation helpers
    private int loadingFrame = 0;

    public LoadoutPopup(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Open(ulong characterId)
    {
        currentCharacterId = characterId;
        isOpen = true;

        // Auto-detect current job
        if (Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc)
        {
            if (!plugin.IsJobExcluded(pc.ClassJob.RowId))
            {
                manualJobId = pc.ClassJob.RowId;
            }
            else
            {
                manualJobId = 0;
            }
        }
        else
        {
            manualJobId = 0;
        }

        // Refresh Etro list if needed
        RefreshEtroList();

        shouldOpen = true;
    }

    private void RefreshEtroList()
    {
        if (manualJobId == 0) return;

        isLoadingEtro = true;
        Task.Run(async () =>
        {
            try
            {
                etroSets = await plugin.BiSManager.GetEtroSets(manualJobId);
                FilterEtroSets();
            }
            finally
            {
                isLoadingEtro = false;
            }
        });
    }

    private void FilterEtroSets()
    {
        if (string.IsNullOrWhiteSpace(etroSearch))
        {
            filteredEtroSets = etroSets.ToList();
        }
        else
        {
            var term = etroSearch.ToLower();
            filteredEtroSets = etroSets.Where(s => s.name.ToLower().Contains(term) || s.creator.ToLower().Contains(term)).ToList();
        }
    }

    public void Draw()
    {
        if (shouldOpen)
        {
            ImGui.OpenPopup("Manage Loadout");
            shouldOpen = false;
        }

        if (!isOpen) return;

        // Tick loading animation
        loadingFrame++;

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

            // ── Top accent line across popup ──
            dl.AddRectFilled(
                windowPos + new Vector2(1, 1),
                windowPos + new Vector2(windowSize.X - 1, 4),
                ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.6f }));

            // Header
            DrawJobSelector(ref manualJobId, "Loadout Job");

            ImGui.Spacing();

            // Styled separator
            DrawAccentSeparator();

            ImGui.Spacing();

            DrawCustomTabBar();

            ImGui.Spacing();

            switch (activeTab)
            {
                case 0: DrawTrackConfigTab(); break;
                case 1: DrawEtroBrowseTab(); break;
                case 2: DrawManualEntry(); break;
                case 3: DrawUrlImport(); break;
            }

            ImGui.Spacing();
            DrawAccentSeparator();
            ImGui.Spacing();

            // Footer Buttons — right aligned
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

    // ── Custom tab bar with proper icon font rendering ──
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

            // Measure icon + label
            ImGui.PushFont(UiBuilder.IconFont);
            var iconStr = icon.ToIconString();
            var iconSize = ImGui.CalcTextSize(iconStr);
            ImGui.PopFont();
            var labelSize = ImGui.CalcTextSize(label);
            float tabWidth = iconSize.X + 6 + labelSize.X + 24; // padding

            var tabMin = new Vector2(x, startPos.Y);
            var tabMax = new Vector2(x + tabWidth, startPos.Y + tabHeight);

            // Hit test
            ImGui.SetCursorScreenPos(tabMin);
            if (i > 0) ImGui.SameLine(0, 0);
            ImGui.InvisibleButton($"##tab_{i}", tabMax - tabMin);
            bool hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) activeTab = i;

            // Background
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

            // Icon
            var textColor = isActive ? Theme.AccentPrimary : (hovered ? Theme.TextPrimary : Theme.TextSecondary);
            var iconPos = new Vector2(tabMin.X + 12, tabMin.Y + (tabHeight - iconSize.Y) * 0.5f);
            dl.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), iconPos, ImGui.GetColorU32(textColor), iconStr);

            // Label
            var labelPos = new Vector2(iconPos.X + iconSize.X + 6, tabMin.Y + (tabHeight - labelSize.Y) * 0.5f);
            dl.AddText(labelPos, ImGui.GetColorU32(textColor), label);

            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            x += tabWidth + 4;
        }

        // Bottom line
        dl.AddLine(
            new Vector2(startPos.X, startPos.Y + tabHeight),
            new Vector2(startPos.X + totalWidth, startPos.Y + tabHeight),
            ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.4f }), 1f);

        ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + tabHeight + 2));
    }

    // ── Styled horizontal separator with accent color ──
    private void DrawAccentSeparator()
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        dl.AddLine(pos, pos + new Vector2(width, 0), ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.25f }), 1.0f);
        ImGui.Dummy(new Vector2(0, 1));
    }

    // ── Section header with subtle background band ──
    private void DrawSectionHeader(string title, string? subtitle = null)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = subtitle != null ? 40f : 26f;

        // Background band
        dl.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.GetColorU32(Theme.SectionHeaderBg), 4f);
        // Left accent bar
        dl.AddRectFilled(pos, pos + new Vector2(3, height), ImGui.GetColorU32(Theme.AccentPrimary), 2f);

        // Title
        dl.AddText(pos + new Vector2(12, 4), ImGui.GetColorU32(Theme.AccentPrimary), title);
        if (subtitle != null)
        {
            dl.AddText(pos + new Vector2(12, 22), ImGui.GetColorU32(Theme.TextSecondary), subtitle);
        }

        ImGui.Dummy(new Vector2(0, height + 6));
    }

    // ── Pill badge via DrawList ──
    private void DrawPill(ImDrawListPtr dl, Vector2 pos, string text, Vector4 color)
    {
        var textSize = ImGui.CalcTextSize(text);
        var padding = new Vector2(8, 3);
        var rectMin = pos;
        var rectMax = pos + textSize + padding * 2;
        dl.AddRectFilled(rectMin, rectMax, ImGui.GetColorU32(color with { W = 0.18f }), 10f);
        dl.AddRect(rectMin, rectMax, ImGui.GetColorU32(color with { W = 0.40f }), 10f);
        dl.AddText(rectMin + padding, ImGui.GetColorU32(color), text);
    }

    // ────────────────────────────────────────────────────────────
    // Track Configuration Tab
    // ────────────────────────────────────────────────────────────
    private void DrawTrackConfigTab()
    {
        ImGui.Spacing();
        DrawSectionHeader("1. Gear Snapshot", "Capture your currently equipped gear for tracking.");

        ImGui.Indent(12);

        bool isCurrentJob = Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc && pc.ClassJob.RowId == manualJobId;

        if (isCurrentJob)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentSuccess);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentSuccess with { W = 0.85f });
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentSuccess with { W = 0.70f });
            if (ImGui.Button("Snapshot Current Gear", new Vector2(260, 36)))
            {
                plugin.TrackCurrentGearset();
            }
            ImGui.PopStyleColor(3);
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
        DrawAccentSeparator();
        ImGui.Spacing();

        DrawSectionHeader("2. BiS Target", "Set a Best-in-Slot loadout to compare against (optional).");

        ImGui.Indent(12);
        ImGui.TextColored(Theme.TextDisabled, "Use the Browse Etro.gg or Import URL tabs to set a BiS target.");

        ImGui.Spacing();

        // BiS status card
        var sdl = ImGui.GetWindowDrawList();
        var statusPos = ImGui.GetCursorScreenPos();
        var statusWidth = ImGui.GetContentRegionAvail().X - 24;

        if (currentCharacterId.HasValue && plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var charData) && charData.BisSets.TryGetValue(manualJobId, out var bis))
        {
            // Active BiS card — single row with name left, pill right
            var cardHeight = 32f;
            sdl.AddRectFilled(statusPos, statusPos + new Vector2(statusWidth, cardHeight), ImGui.GetColorU32(Theme.AccentSuccess with { W = 0.08f }), Theme.RowRounding);
            sdl.AddRect(statusPos, statusPos + new Vector2(statusWidth, cardHeight), ImGui.GetColorU32(Theme.AccentSuccess with { W = 0.25f }), Theme.RowRounding);
            sdl.AddRectFilled(statusPos, statusPos + new Vector2(3, cardHeight), ImGui.GetColorU32(Theme.AccentSuccess), 2f);

            // Source pill — right-aligned (draw first to know reserved width)
            var pillText = bis.SourceType;
            var pillTextSize = ImGui.CalcTextSize(pillText);
            var pillPadding = new Vector2(8, 3);
            var pillTotalWidth = pillTextSize.X + pillPadding.X * 2;
            var pillPos = statusPos + new Vector2(statusWidth - pillTotalWidth - 14, (cardHeight - pillTextSize.Y - pillPadding.Y * 2) * 0.5f);
            DrawPill(sdl, pillPos, pillText, Theme.AccentSecondary);

            // Truncate name if too wide (leave room for pill)
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
                charData.BisSets.Remove(manualJobId);
                plugin.Configuration.Save();
            }
            ImGui.PopStyleColor(3);
        }
        else
        {
            // Empty state
            sdl.AddRectFilled(statusPos, statusPos + new Vector2(statusWidth, 36), ImGui.GetColorU32(Theme.BgDark), Theme.RowRounding);
            sdl.AddRect(statusPos, statusPos + new Vector2(statusWidth, 36), ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), Theme.RowRounding, ImDrawFlags.None, 1f);
            sdl.AddText(statusPos + new Vector2(14, 10), ImGui.GetColorU32(Theme.TextDisabled), "No BiS target set — tracking gear only");
            ImGui.Dummy(new Vector2(0, 40));
        }

        ImGui.Unindent(12);
    }

    // ────────────────────────────────────────────────────────────
    // Browse Etro.gg Tab
    // ────────────────────────────────────────────────────────────
    private void DrawEtroBrowseTab()
    {
        ImGui.Spacing();

        var dl = ImGui.GetWindowDrawList();

        // ── Search bar row ──
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 130);
        if (ImGui.InputTextWithHint("##etroSearch", "Search by name or creator...", ref etroSearch, 64))
        {
            FilterEtroSets();
        }
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.20f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.35f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.50f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentPrimary);
        if (ImGui.Button("Refresh", new Vector2(110, 0)))
        {
            RefreshEtroList();
        }
        ImGui.PopStyleColor(4);

        ImGui.Spacing();

        // ── Result count pill ──
        if (!isLoadingEtro && filteredEtroSets.Any())
        {
            var countText = $"{filteredEtroSets.Count} set{(filteredEtroSets.Count != 1 ? "s" : "")}";
            DrawPill(dl, ImGui.GetCursorScreenPos(), countText, Theme.AccentSecondary);
            ImGui.Dummy(new Vector2(0, 22));
        }

        // ── Loading state ──
        if (isLoadingEtro)
        {
            DrawLoadingIndicator("Fetching sets from Etro.gg...");
            return;
        }

        // ── Empty state ──
        if (!filteredEtroSets.Any())
        {
            ImGui.Spacing();
            DrawEmptyState(manualJobId == 0 ? "Select a job above to browse Etro.gg sets." : "No sets found. Try adjusting your search.");
            return;
        }

        // ── Scrollable set list ──
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.RowRounding);
        if (ImGui.BeginChild("EtroList", new Vector2(0, 350), false))
        {
            foreach (var set in filteredEtroSets)
            {
                DrawEtroSetRow(set);
            }
            ImGui.EndChild();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    private void DrawEtroSetRow(EtroBiSSet set)
    {
        var dl = ImGui.GetWindowDrawList();
        var rowHeight = 38f;
        var contentWidth = ImGui.GetContentRegionAvail().X;

        ImGui.PushID(set.id);

        var startPos = ImGui.GetCursorScreenPos();
        var storage = ImGui.GetStateStorage();
        var storageId = ImGui.GetID("IsExpanded");
        bool isExpanded = storage.GetBool(storageId, false);

        if (ImGui.InvisibleButton($"##sel{set.id}", new Vector2(contentWidth, rowHeight)))
        {
            storage.SetBool(storageId, !isExpanded);
        }

        bool isHovered = ImGui.IsItemHovered();
        if (isHovered)
        {
            ImGui.SetTooltip("Click to view details");
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rowEnd = startPos + new Vector2(contentWidth, rowHeight);
        var rowBg = isHovered ? Theme.EtroRowHover : Theme.EtroRowBg;
        
        dl.AddRectFilled(startPos, rowEnd, ImGui.GetColorU32(rowBg), Theme.RowRounding);

        if (isHovered)
            dl.AddRectFilled(startPos, startPos + new Vector2(3, rowHeight), ImGui.GetColorU32(Theme.ProviderEtro), 2f);

        // Name
        var nameText = set.name;
        // Truncate name if it's too long
        var maxNameWidth = contentWidth * 0.4f;
        if (ImGui.CalcTextSize(nameText).X > maxNameWidth)
        {
            while (nameText.Length > 3 && ImGui.CalcTextSize(nameText + "...").X > maxNameWidth)
                nameText = nameText[..^1];
            nameText += "...";
        }
        dl.AddText(startPos + new Vector2(14, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(Theme.TextPrimary), nameText);

        // ── Compact pill layout: IL | GCD | Patch ──
        float pillX = startPos.X + 14 + maxNameWidth + 20;
        float pillY = startPos.Y + (rowHeight - 20) * 0.5f;

        var avgIl = set.AverageItemLevel;
        var ilText = $"IL {avgIl}";
        var ilColor = avgIl >= Theme.ILThresholdMax ? Theme.ILTierMax
                    : avgIl >= Theme.ILThresholdHigh ? Theme.AccentSuccess
                    : Theme.AccentWarning;
        DrawPill(dl, new Vector2(pillX, pillY), ilText, ilColor);
        pillX += ImGui.CalcTextSize(ilText).X + 24;

        if (set.gcd > 0)
        {
            var gcdText = $"GCD {set.gcd:F2}";
            DrawPill(dl, new Vector2(pillX, pillY), gcdText, Theme.AccentSecondary);
            pillX += ImGui.CalcTextSize(gcdText).X + 24;
        }

        if (set.patch > 0)
        {
            var patchText = $"Patch {set.patch:F1}";
            var patchSize = ImGui.CalcTextSize(patchText);
            var pillPad = 16f; // pill internal padding (8 * 2)
            var patchX = rowEnd.X - patchSize.X - pillPad - 28;
            DrawPill(dl, new Vector2(patchX, pillY), patchText, Theme.TextSecondary);
        }

        var arrowText = isExpanded ? "▼" : "▶";
        var arrowSize = ImGui.CalcTextSize(arrowText);
        var arrowPos = startPos + new Vector2(contentWidth - arrowSize.X - 10, (rowHeight - arrowSize.Y) * 0.5f);
        dl.AddText(arrowPos, ImGui.GetColorU32(Theme.TextDisabled), arrowText);

        if (isExpanded)
        {
            ImGui.Spacing();
            ImGui.Indent(12);

            // Fetch gear details on expand
            plugin.BiSManager.FetchEtroGearsetDetail(set.id);
            var detail = plugin.BiSManager.GetCachedGearsetDetail(set.id);

            if (detail != null)
            {
                // Two-column gear layout
                int[] leftSlots = { 0, 1, 2, 3, 4, 6, 7 };
                int[] rightSlots = { 8, 9, 10, 11, 12 };

                if (ImGui.BeginTable($"##etroGear{set.id}", 2, ImGuiTableFlags.None, new Vector2(contentWidth - 24, 0)))
                {
                    ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    foreach (var slotId in leftSlots)
                    {
                        if (detail.Items.TryGetValue(slotId, out var slot))
                        {
                            SharedDrawHelpers.DrawItemIcon(slot.ItemId, 20f);
                            ImGui.SameLine();
                            ImGui.TextColored(Theme.TextSecondary, $"{slot.Name}");
                        }
                    }

                    ImGui.TableNextColumn();
                    foreach (var slotId in rightSlots)
                    {
                        if (detail.Items.TryGetValue(slotId, out var slot))
                        {
                            SharedDrawHelpers.DrawItemIcon(slot.ItemId, 20f);
                            ImGui.SameLine();
                            ImGui.TextColored(Theme.TextSecondary, $"{slot.Name}");
                        }
                    }

                    // Food row
                    if (detail.FoodId != 0)
                    {
                        SharedDrawHelpers.DrawItemIcon(detail.FoodId, 20f);
                        ImGui.SameLine();
                        var foodSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                        if (foodSheet != null && foodSheet.TryGetRow(detail.FoodId, out var foodRow))
                            ImGui.TextColored(Theme.AccentPrimary, foodRow.Name.ToString());
                    }

                    ImGui.EndTable();
                }

                ImGui.Spacing();
            }
            else
            {
                ImGui.TextColored(Theme.TextDisabled, "Loading gear details...");
                ImGui.Spacing();
            }

            // Import button
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.2f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.4f });
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.6f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentPrimary);

            if (ImGui.Button("Import This Loadout Target", new Vector2(240, 32)))
            {
                plugin.BiSManager.ImportFromUrl($"https://etro.gg/gearset/{set.id}", manualJobId);
                isOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor(4);

            ImGui.Spacing();
            ImGui.Unindent(12);
        }

        ImGui.PopID();
        ImGui.Dummy(new Vector2(0, 2));
    }

    // ────────────────────────────────────────────────────────────
    // Import URL Tab
    // ────────────────────────────────────────────────────────────
    private void DrawUrlImport()
    {
        ImGui.Spacing();

        DrawSectionHeader("Paste a Gearset Link", "Import from Etro.gg or XIVGear.app");

        ImGui.Spacing();
        ImGui.Indent(12);

        // Provider hints
        var hintDl = ImGui.GetWindowDrawList();
        var hintPos = ImGui.GetCursorScreenPos();
        DrawPill(hintDl, hintPos, "etro.gg", Theme.ProviderEtro);
        var etroWidth = ImGui.CalcTextSize("etro.gg").X + 22;
        DrawPill(hintDl, hintPos + new Vector2(etroWidth + 8, 0), "xivgear.app", Theme.ProviderXIVGear);
        ImGui.Dummy(new Vector2(0, 26));

        ImGui.Spacing();

        // URL input field — full width, styled
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

        // Import button
        bool canImport = !string.IsNullOrWhiteSpace(importUrl) && manualJobId != 0;

        if (!canImport) ImGui.BeginDisabled();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.70f });
        if (ImGui.Button("Import Gearset", new Vector2(180, 38)))
        {
             if (currentCharacterId.HasValue && plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var data))
             {
                 plugin.BiSManager.ImportFromUrl(importUrl, manualJobId);
                 isOpen = false;
                 ImGui.CloseCurrentPopup();
             }
        }
        ImGui.PopStyleColor(3);

        if (!canImport) ImGui.EndDisabled();

        if (!canImport && manualJobId == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.AccentWarning, "Select a job above before importing.");
        }

        ImGui.Unindent(12);
    }

    // ────────────────────────────────────────────────────────────
    // Manual Builder Tab
    // ────────────────────────────────────────────────────────────
    private void DrawManualEntry()
    {
        ImGui.Spacing();
        InitializeIlRange();

        // ── IL Range (configurable) ──
        DrawSectionHeader("Item Level Range");
        ImGui.Indent(12);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 4));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);

        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("##ilMin", ref manualMinIl, 0, 0))
        {
            if (manualMinIl < 1) manualMinIl = 1;
            if (manualMinIl > manualMaxIl) manualMinIl = manualMaxIl;
            RebuildItemList();
        }
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextSecondary, "–");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("##ilMax", ref manualMaxIl, 0, 0))
        {
            if (manualMaxIl < manualMinIl) manualMaxIl = manualMinIl;
            RebuildItemList();
        }
        ImGui.SameLine(0, 12);
        if (manualMinIl != defaultMinIl || manualMaxIl != defaultMaxIl)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.15f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.30f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentPrimary);
            if (ImGui.Button("Reset", new Vector2(50, 0)))
            {
                manualMinIl = defaultMinIl;
                manualMaxIl = defaultMaxIl;
                RebuildItemList();
            }
            ImGui.PopStyleColor(3);
        }

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        ImGui.Unindent(12);

        ImGui.Spacing();
        DrawAccentSeparator();
        ImGui.Spacing();

        // ── Slot Selection Grid (with armory icons) ──
        DrawSectionHeader("Gear Slot");
        ImGui.Indent(12);
        DrawSlotGrid();
        ImGui.Unindent(12);

        ImGui.Spacing();
        DrawAccentSeparator();
        ImGui.Spacing();

        // ── Item Selector (combo box) ──
        DrawSectionHeader("Select Item", $"{SharedDrawHelpers.GetSlotName(manualSlotId)} slot");
        ImGui.Indent(12);

        if (manualJobId == 0)
        {
            ImGui.TextColored(Theme.AccentWarning, "Select a job above to see available items.");
        }
        else
        {
            // Rebuild list if empty (first open or slot/job changed)
            if (manualSearchResults.Count == 0 && manualJobId > 0)
                RebuildItemList();

            var preview = selectedItemIndex >= 0 && selectedItemIndex < manualSearchResults.Count
                ? manualSearchResults[selectedItemIndex].Name.ToString()
                : "Select an item...";

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
            ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
            if (ImGui.BeginCombo("##itemSelector", preview))
            {
                for (int i = 0; i < manualSearchResults.Count; i++)
                {
                    var item = manualSearchResults[i];
                    var itemName = item.Name.ToString();
                    var il = item.LevelItem.RowId;
                    bool isSelected = selectedItemIndex == i;

                    if (ImGui.Selectable($"{itemName}  (IL {il})##item{item.RowId}", isSelected))
                    {
                        selectedItemIndex = i;
                        plugin.BiSManager.SetBiSItem(manualJobId, manualSlotId, item.RowId, itemName, il, null!);
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);

            if (!manualSearchResults.Any())
            {
                ImGui.Spacing();
                ImGui.TextColored(Theme.TextDisabled, "No items found for this slot and job.");
            }
        }

        ImGui.Unindent(12);

        // Materia (if item exists)
        if (currentCharacterId.HasValue &&
            plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var data) &&
            data.BisSets.TryGetValue(manualJobId, out var bisSet) &&
            bisSet.Items.TryGetValue(manualSlotId, out var bisItem))
        {
            ImGui.Spacing();
            DrawAccentSeparator();
            ImGui.Spacing();

            // Determine materia slot count from item data
            int materiaSlots = 5;
            var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            if (itemSheet != null && itemSheet.TryGetRow(bisItem.ItemId, out var itemRow))
            {
                materiaSlots = itemRow.MateriaSlotCount;
                if (itemRow.IsAdvancedMeldingPermitted)
                    materiaSlots = 5;
            }

            DrawSectionHeader($"Materia Melds — {bisItem.Name}");
            ImGui.Indent(12);
            DrawMateriaSelector(bisItem, materiaSlots);
            ImGui.Unindent(12);
        }

        // ── Food selector ──
        if (currentCharacterId.HasValue &&
            plugin.Configuration.Characters.TryGetValue(currentCharacterId.Value, out var foodCharData))
        {
            ImGui.Spacing();
            DrawAccentSeparator();
            ImGui.Spacing();

            DrawSectionHeader("Food");
            ImGui.Indent(12);
            DrawFoodSelector(foodCharData);
            ImGui.Unindent(12);
        }
    }

    private void DrawFoodSelector(CharacterData charData)
    {
        if (!foodListBuilt)
        {
            BuildFoodList();
            foodListBuilt = true;
        }

        charData.BisSets.TryGetValue(manualJobId, out var bisSet);
        uint currentFoodId = bisSet?.FoodId ?? 0;

        string preview = "None";
        if (currentFoodId != 0)
        {
            var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            if (sheet != null && sheet.TryGetRow(currentFoodId, out var foodRow))
                preview = foodRow.Name.ToString();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.InputFieldBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.InputFieldBorder);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
        if (ImGui.BeginCombo("##foodSelector", preview))
        {
            if (ImGui.Selectable("None", currentFoodId == 0))
            {
                plugin.BiSManager.SetBiSFood(manualJobId, 0);
            }

            foreach (var food in foodSearchResults)
            {
                var name = food.Name.ToString();
                var il = food.LevelItem.RowId;
                bool isSelected = food.RowId == currentFoodId;

                if (ImGui.Selectable($"{name}  (IL {il})##food{food.RowId}", isSelected))
                {
                    plugin.BiSManager.SetBiSFood(manualJobId, food.RowId);
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void BuildFoodList()
    {
        foodSearchResults.Clear();
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        foreach (var item in sheet)
        {
            if (item.ItemUICategory.RowId != 46) continue; // 46 = Meal
            if (item.LevelItem.RowId < 690) continue; // Current-tier food only
            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains("Seafood Stew") && item.LevelItem.RowId < 610) continue; // filter old dupes

            foodSearchResults.Add(item);
        }

        foodSearchResults = foodSearchResults.OrderByDescending(f => f.LevelItem.RowId).ThenBy(f => f.Name.ToString()).ToList();
    }

    private void InitializeIlRange()
    {
        if (ilRangeInitialized) return;
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        int maxIl = 0;
        foreach (var item in sheet)
        {
            if (item.EquipSlotCategory.Value.RowId != 0 && (int)item.LevelItem.RowId > maxIl)
                maxIl = (int)item.LevelItem.RowId;
        }

        if (maxIl > 0)
        {
            manualMaxIl = maxIl;
            manualMinIl = maxIl - 20;
            defaultMaxIl = maxIl;
            defaultMinIl = maxIl - 20;
        }
        ilRangeInitialized = true;
    }

    private void RebuildItemList()
    {
        manualSearchResults.Clear();
        selectedItemIndex = -1;
        if (manualJobId == 0) return;

        InitializeIlRange();

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet == null) return;

        foreach (var item in sheet)
        {
            if (item.LevelItem.RowId < (uint)manualMinIl || item.LevelItem.RowId > (uint)manualMaxIl) continue;
            if (!IsItemForSlot(item, manualSlotId)) continue;
            if (!IsItemForJob(item, manualJobId)) continue;

            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            manualSearchResults.Add(item);
            if (manualSearchResults.Count >= 100) break;
        }
    }

    private bool IsItemForJob(Lumina.Excel.Sheets.Item item, uint jobId)
    {
        var catRow = item.ClassJobCategory.Value;
        if (catRow.RowId == 0) return false;

        var classJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        if (classJobSheet == null || !classJobSheet.TryGetRow(jobId, out var job)) return true;

        var abbr = job.Abbreviation.ToString();
        // ClassJobCategory has boolean properties named by job abbreviation
        // We use reflection to check the property
        var prop = catRow.GetType().GetProperty(abbr);
        if (prop != null && prop.PropertyType == typeof(bool))
            return (bool)prop.GetValue(catRow)!;

        return true; // fallback: allow if we can't check
    }

    private bool IsItemForSlot(Lumina.Excel.Sheets.Item item, int slotId)
    {
        if (item.EquipSlotCategory.Value.RowId == 0) return false;
        var cat = item.EquipSlotCategory.Value;

        return slotId switch
        {
            0 => cat.MainHand == 1,
            1 => cat.OffHand == 1,
            2 => cat.Head == 1,
            3 => cat.Body == 1,
            4 => cat.Gloves == 1,
            6 => cat.Legs == 1,
            7 => cat.Feet == 1,
            8 => cat.Ears == 1,
            9 => cat.Neck == 1,
            10 => cat.Wrists == 1,
            11 or 12 => cat.FingerL == 1 || cat.FingerR == 1,
            _ => false
        };
    }

    private void DrawSlotGrid()
    {
        int[] slots = { 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12 };
        float btnSize = 40f;
        float btnH = 40f;

        var style = ImGui.GetStyle();
        var spacing = style.ItemSpacing.X;
        var windowVisibleX = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;

        foreach (var s in slots)
        {
            var label = SharedDrawHelpers.GetSlotName(s);
            bool isSelected = manualSlotId == s;

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary with { W = 0.30f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentPrimary with { W = 0.45f });
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentPrimary with { W = 0.55f });
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Theme.SlotBtnBg);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.SlotBtnHover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.BgCardHover);
            }

            // Render slot button with armory icon overlay
            bool clicked = false;
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 0)); // Hide text, we draw icon instead
            clicked = ImGui.Button($"##slot_{s}", new Vector2(btnSize, btnH));
            ImGui.PopStyleColor();

            // Draw armory icon on top of button
            if (SlotIconUVs.TryGetValue(s, out var uv))
            {
                var tex = Plugin.TextureProvider.GetFromGame("ui/uld/ArmouryBoard_hr1.tex");
                if (tex.TryGetWrap(out var wrap, out _))
                {
                    var dl2 = ImGui.GetWindowDrawList();
                    var bMin = ImGui.GetItemRectMin();
                    var bMax = ImGui.GetItemRectMax();
                    var iconSz = new Vector2(btnSize - 10, btnH - 10);
                    var iconOff = (new Vector2(btnSize, btnH) - iconSz) * 0.5f;
                    var tint = isSelected
                        ? ImGui.GetColorU32(new Vector4(0.7f, 0.6f, 1f, 1f))
                        : ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 0.8f, 0.8f));
                    dl2.AddImage(wrap.Handle, bMin + iconOff, bMin + iconOff + iconSz,
                        new Vector2(uv.X, uv.Y), new Vector2(uv.Z, uv.W), tint);
                }
            }

            if (clicked)
            {
                manualSlotId = s;
                RebuildItemList();
            }

            // Draw bottom accent bar on selected
            if (isSelected)
            {
                var dl = ImGui.GetWindowDrawList();
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                dl.AddRectFilled(
                    new Vector2(min.X + 4, max.Y - 3),
                    new Vector2(max.X - 4, max.Y),
                    ImGui.GetColorU32(Theme.AccentPrimary),
                    2f);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(label);

            ImGui.PopStyleColor(3);

            float nextBtnX = ImGui.GetItemRectMax().X + spacing + btnSize;
            if (s < 12 && nextBtnX < windowVisibleX) ImGui.SameLine();
        }
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s[1..];
    }

    private void DrawJobSelector(ref uint jobId, string label)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        var currentJob = sheet?.GetRow(jobId);
        var preview = currentJob != null ? ToTitleCase(currentJob.Value.Name.ToString()) : "Select Job...";

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.TextSecondary, label);
        ImGui.SameLine();

        // Job icon beside the combo
        if (jobId > 0)
        {
            uint jobIconId = 62100u + jobId;
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

                     bool isSelected = jobId == job.RowId;
                     if (ImGui.Selectable(ToTitleCase(job.Name.ToString()), isSelected))
                     {
                         jobId = job.RowId;
                         RefreshEtroList();
                         RebuildItemList();
                     }
                     if (isSelected) ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    private void DrawMateriaSelector(BiSItem item, int slotCount = 5)
    {
        var dl = ImGui.GetWindowDrawList();
        if (slotCount < 1) slotCount = 2;

        for (int i = 0; i < slotCount; i++)
        {
            var matId = item.Materia.Count > i ? item.Materia[i] : 0;
            ImGui.PushID(i);

            bool hasMat = matId > 0;
            var btnColor = hasMat ? Theme.AccentSecondary with { W = 0.20f } : Theme.SlotBtnBg;
            var btnHover = hasMat ? Theme.AccentSecondary with { W = 0.35f } : Theme.SlotBtnHover;
            var textColor = hasMat ? Theme.AccentSecondary : Theme.TextDisabled;

            ImGui.PushStyleColor(ImGuiCol.Button, btnColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, btnHover);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);

            var btnLabel = hasMat ? $"M{i+1}" : "+";
            if (ImGui.Button(btnLabel, new Vector2(36, 34)))
            {
                ImGui.OpenPopup("MateriaPicker");
            }
            ImGui.PopStyleColor(3);

            // Draw subtle indicator dot
            if (hasMat)
            {
                var max = ImGui.GetItemRectMax();
                dl.AddCircleFilled(new Vector2(max.X - 4, ImGui.GetItemRectMin().Y + 4), 3f, ImGui.GetColorU32(Theme.AccentSecondary));
            }

            if (ImGui.IsItemHovered() && hasMat) ImGui.SetTooltip($"Materia Slot {i+1} — ID: {matId}");

            if (ImGui.BeginPopup("MateriaPicker"))
            {
                ImGui.TextColored(Theme.AccentPrimary, $"Slot {i+1} Materia");
                ImGui.Separator();
                ImGui.Spacing();

                if (hasMat)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentDanger);
                    if (ImGui.Selectable("Clear")) UpdateMateria(item, i, 0);
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.TextColored(Theme.TextSecondary, "Grade XII");
                if (ImGui.Selectable("  Crit XII"))  UpdateMateria(item, i, 44103);
                if (ImGui.Selectable("  Det XII"))   UpdateMateria(item, i, 44104);
                if (ImGui.Selectable("  DH XII"))    UpdateMateria(item, i, 44105);
                if (ImGui.Selectable("  SpS XII"))   UpdateMateria(item, i, 44106);
                if (ImGui.Selectable("  SkS XII"))   UpdateMateria(item, i, 44107);

                ImGui.Spacing();
                ImGui.TextColored(Theme.TextSecondary, "Grade XI");
                if (ImGui.Selectable("  Crit XI"))   UpdateMateria(item, i, 44098);
                if (ImGui.Selectable("  Det XI"))    UpdateMateria(item, i, 44099);
                if (ImGui.Selectable("  DH XI"))     UpdateMateria(item, i, 44100);
                if (ImGui.Selectable("  SpS XI"))    UpdateMateria(item, i, 44101);
                if (ImGui.Selectable("  SkS XI"))    UpdateMateria(item, i, 44102);

                ImGui.EndPopup();
            }
            ImGui.PopID();
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void UpdateMateria(BiSItem item, int slotIndex, uint materiaId)
    {
        while (item.Materia.Count <= slotIndex) item.Materia.Add(0);
        item.Materia[slotIndex] = materiaId;
        plugin.BiSManager.SetBiSItem(manualJobId, item.Slot, item.ItemId, item.Name, item.ItemLevel, item.Materia);
    }

    // ── Loading spinner indicator ──
    private void DrawLoadingIndicator(string message)
    {
        ImGui.Spacing();
        ImGui.Spacing();

        var dl = ImGui.GetWindowDrawList();
        var center = ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X * 0.5f, 30);

        // Animated dots
        var dots = new string('.', (loadingFrame / 20) % 4);
        var fullText = $"{message}{dots}";
        var textSize = ImGui.CalcTextSize(fullText);
        dl.AddText(center - new Vector2(textSize.X * 0.5f, 0), ImGui.GetColorU32(Theme.AccentPrimary), fullText);

        // Spinning arc
        float radius = 8f;
        var arcCenter = center + new Vector2(0, 25);
        float startAngle = (loadingFrame * 0.05f) % (MathF.PI * 2);
        dl.AddCircle(arcCenter, radius, ImGui.GetColorU32(Theme.AccentPrimary with { W = 0.15f }), 20, 2f);

        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = startAngle + (MathF.PI * 2 * i / segments);
            float nextAngle = startAngle + (MathF.PI * 2 * (i + 1) / segments);
            if (i < segments / 3)
            {
                var p1 = arcCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                var p2 = arcCenter + new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * radius;
                dl.AddLine(p1, p2, ImGui.GetColorU32(Theme.AccentPrimary), 2.5f);
            }
        }

        ImGui.Dummy(new Vector2(0, 65));
    }

    // ── Empty state placeholder ──
    private void DrawEmptyState(string message)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 60f;

        dl.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.GetColorU32(Theme.BgDark), Theme.RowRounding);
        dl.AddRect(pos, pos + new Vector2(width, height), ImGui.GetColorU32(Theme.BorderSubtle with { W = 0.3f }), Theme.RowRounding, ImDrawFlags.None, 1f);

        var textSize = ImGui.CalcTextSize(message);
        dl.AddText(
            pos + new Vector2((width - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f),
            ImGui.GetColorU32(Theme.TextDisabled),
            message);

        ImGui.Dummy(new Vector2(0, height + 4));
    }

    public void Dispose()
    {
        manualSearchResults.Clear();
        etroSets.Clear();
        filteredEtroSets.Clear();
    }
}
