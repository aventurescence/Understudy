using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using Understudy.Managers;
using Understudy.Windows.Components;

namespace Understudy.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Sidebar sidebar;
    private readonly LoadoutPopup loadoutPopup;
    private readonly Dashboard dashboard;
    private readonly CharacterDetail characterDetail;
    private readonly SettingsView settingsView;
    private enum ViewType { Dashboard, Character, Settings }
    private ViewType _currentView = ViewType.Dashboard;
    private ViewType currentView
    {
        get => _currentView;
        set
        {
            if (_currentView != value)
            {
                _currentView = value;
                RefreshSizeConstraints();
            }
        }
    }
    private ulong? selectedCharacterId;

    public MainWindow(Plugin plugin) : base("Understudy")
    {
        this.plugin = plugin;
        loadoutPopup = new LoadoutPopup(plugin);
        settingsView = new SettingsView(plugin);
        
        characterDetail = new CharacterDetail(plugin, 
            () => // On Back
            {
                currentView = ViewType.Dashboard;
                selectedCharacterId = null;
                sidebar?.UpdateSelection(null);
                plugin.Configuration.Save();
            },
            loadoutPopup
        );
        
        dashboard = new Dashboard(plugin,
            id => // On Select Character
            {
                selectedCharacterId = id;
                currentView = ViewType.Character;
                sidebar?.UpdateSelection(id);
                characterDetail.SetCharacter(id);
            }
        );
        dashboard.OnSelectedCharacterDeleted += () =>
        {
            selectedCharacterId = null;
            currentView = ViewType.Dashboard;
            sidebar?.UpdateSelection(null);
        };
        
        sidebar = new Sidebar(plugin, 
            id => // On Selection Changed (Character)
            {
                if (id != null)
                {
                    selectedCharacterId = id;
                    currentView = ViewType.Character;
                    characterDetail.SetCharacter(id.Value);
                }
                else
                {
                    selectedCharacterId = null;
                    currentView = ViewType.Dashboard;
                }
            },
            () => // On Settings/Config Requested
            {
                currentView = ViewType.Settings;
                selectedCharacterId = null; 
                sidebar?.UpdateSelection(null); // Clear character selection visual
            }
        );

        RefreshSizeConstraints();
    }

    private void RefreshSizeConstraints()
    {
        var scale = ImGui.GetIO().FontGlobalScale;
        if (currentView == ViewType.Dashboard || currentView == ViewType.Settings)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600f * scale, 400f * scale),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }
        else
        {
            // Keep the larger constraint for Character Detail as requested
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(1250, 650),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }
    }

    /// <summary>
    /// Called by the windowing system when the window becomes visible.
    /// Refreshes character data so the dashboard is always up-to-date.
    /// </summary>
    public override void OnOpen()
    {
        plugin.UpdateCharacterData();
        RefreshSizeConstraints(); // Ensure constraints match scale on open
    }
    
    public override void Draw()
    {
        // ── Sidebar ──────────────────────────────────────────────────
        var sidebarWidth = 240f * ImGui.GetIO().FontGlobalScale; // Increased width
        
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.BgDark);
        ImGui.BeginChild("Sidebar", new Vector2(sidebarWidth, 0), true);
        
        sidebar.Draw();
        
        ImGui.EndChild();
        ImGui.PopStyleColor();
        
        ImGui.SameLine(0, 4f);
        
        // ── Content Area ─────────────────────────────────────────────
        ImGui.BeginGroup();
        
        ImGui.BeginChild("Content", new Vector2(0, 0), false);

        switch (currentView)
        {
            case ViewType.Dashboard:
                dashboard.Draw();
                break;
            case ViewType.Character:
                characterDetail.Draw();
                break;
            case ViewType.Settings:
                settingsView.Draw();
                break;
        }
        
        ImGui.EndChild();
        ImGui.EndGroup();
        
        loadoutPopup.Draw();
    }

    public void Dispose()
    {
        loadoutPopup.Dispose();
    }
}
