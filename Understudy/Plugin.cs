using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Collections.Generic;
using System;
using System.Net.Http;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Understudy.Windows;
using Understudy.Managers;

namespace Understudy;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/understudy";

    public Configuration Configuration { get; init; }
    public TomestoneManager TomestoneManager { get; init; }
    public GearManager GearManager { get; init; }
    public RaidManager RaidManager { get; init; }
    public MiscellanyManager MiscellanyManager { get; init; }
    public BiSManager BiSManager { get; init; }
    public MateriaTextureManager MateriaTextures { get; init; }

    internal readonly HttpClient HttpClient = new();

    public readonly WindowSystem WindowSystem = new("Understudy");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public ulong CurrentContentId { get; private set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        TomestoneManager = new TomestoneManager(DataManager);
        GearManager = new GearManager(DataManager, ObjectTable);
        RaidManager = new RaidManager(DataManager);
        MiscellanyManager = new MiscellanyManager(DataManager, Log);
        BiSManager = new BiSManager(this, DataManager, Log);
        MateriaTextures = new MateriaTextureManager(this);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Understudy main window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

        Log.Information("Understudy initialized.");

        // Check if already logged in (e.g. plugin reloaded while game running)
        // Must run on Framework thread to access ObjectTable/PlayerState safely
        if (ClientState.IsLoggedIn)
        {
            Framework.Update += OnFrameworkUpdate;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Run once then unsubscribe
        Framework.Update -= OnFrameworkUpdate;
        
        try 
        {
             OnLogin();
        }
        catch (Exception ex)
        {
             Log.Error(ex, "Failed to initialize data on startup via Framework.Update.");
        }
    }
    
    private void OnLogin()
    {
        CurrentContentId = PlayerState.ContentId;
        UpdateCharacterData();
    }
    
    private void OnLogout(int type, int code)
    {
        CurrentContentId = 0;
    }

    private static readonly HashSet<uint> ExcludedJobs = new()
    {
        // Base Classes
        1,  // GLD
        3,  // MRD
        4,  // LNC
        5,  // ARC
        6,  // CNJ
        7,  // THM
        26, // ACN
        29, // ROG
        
        // DoH
        8, 9, 10, 11, 12, 13, 14, 15,
        
        // DoL
        16, 17, 18
    };

    public bool IsJobExcluded(uint jobId) => ExcludedJobs.Contains(jobId);

    public void UpdateCharacterData()
    {
        if (CurrentContentId == 0) return;
        
        if (!Configuration.Characters.ContainsKey(CurrentContentId))
        {
            var p = ObjectTable[0] as IPlayerCharacter;
            Configuration.Characters[CurrentContentId] = new CharacterData
            {
                Name = p?.Name.ToString() ?? "Unknown",
                WorldId = p?.HomeWorld.RowId ?? 0,
                ContentId = CurrentContentId
            };
        }
        
        var charData = Configuration.Characters[CurrentContentId];
        charData.Tomestones = TomestoneManager.UpdateTomestones();
        
        // Gear is NOT auto-tracked. Player must explicitly click "+" to track.
        
        // Cleanup excluded jobs from existing data
        foreach (var id in ExcludedJobs)
        {
            charData.GearSets.Remove(id);
            charData.BisSets.Remove(id);
        }
        
        // Raid Progress: Use WeeklyLockoutInfo from PlayerState
        charData.RaidProgress = RaidManager.GetRaidDataFromLockoutInfo();
        charData.Miscellany = MiscellanyManager.ScanInventories();
        
        Configuration.Save();
    }

    /// <summary>
    /// Manually captures the currently equipped gearset and adds it to the
    /// active character's tracked gear sets. Called when the user clicks "+".
    /// </summary>
    public void TrackCurrentGearset()
    {
        if (CurrentContentId == 0) return;
        if (!Configuration.Characters.TryGetValue(CurrentContentId, out var charData)) return;

        var gear = GearManager.UpdateGear();
        if (gear.JobId == 0) return;

        if (ExcludedJobs.Contains(gear.JobId))
        {
            Log.Information($"Skipping track for excluded job {gear.JobId}");
            return;
        }

        charData.GearSets[gear.JobId] = gear;
        Configuration.Save();
        Log.Information("Tracked gearset for job {JobId} (IL {IL})", gear.JobId, gear.AverageItemLevel);
    }

    public void RunOnFramework(Action action)
    {
        // Wrapper to auto-unsubscribe
        var handler = new FrameworkHandler(action, Framework);
        Framework.Update += handler.OnUpdate;
    }

    private class FrameworkHandler
    {
        private readonly Action _action;
        private readonly IFramework _framework;

        public FrameworkHandler(Action action, IFramework framework)
        {
            _action = action;
            _framework = framework;
        }

        public void OnUpdate(IFramework framework)
        {
            _framework.Update -= OnUpdate;
            try
            {
                _action();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in RunOnFramework action");
            }
        }
    }

    public void CheckAndTrackGear(uint jobId)
    {
        RunOnFramework(() =>
        {
            if (CurrentContentId == 0) return;
            if (!Configuration.Characters.TryGetValue(CurrentContentId, out var charData)) return;

            // If we already have a gearset for this job, do nothing
            if (charData.GearSets.ContainsKey(jobId)) return;

            // Check if player is currently on this job
            if (Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is IPlayerCharacter player && player.ClassJob.RowId == jobId)
            {
                Log.Information("Auto-tracking gear for imported BiS job {JobId}", jobId);
                TrackCurrentGearset();
            }
        });
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        
        MateriaTextures.Dispose();
        HttpClient.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
