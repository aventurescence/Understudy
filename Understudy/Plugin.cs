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

    internal static TierDiscovery? TierDiscovery { get; private set; }

    public Configuration Configuration { get; init; }
    public TomestoneManager TomestoneManager { get; init; }
    public GearManager GearManager { get; init; }
    public RaidManager RaidManager { get; init; }
    public MiscellanyManager MiscellanyManager { get; init; }
    public BiSManager BiSManager { get; init; }
    public StatCalculator StatCalculator { get; init; }
    public MateriaTextureManager MateriaTextures { get; init; }

    internal readonly HttpClient HttpClient = new();

    public readonly WindowSystem WindowSystem = new("Understudy");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public ulong CurrentContentId { get; private set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (Configuration.CharacterOrder.Count == 0 && Configuration.Characters.Count > 0)
        {
            Configuration.CharacterOrder.AddRange(Configuration.Characters.Keys);
            Configuration.Save();
        }

        TierDiscovery = new TierDiscovery(DataManager, Log);
        TierConfig.Initialize(TierDiscovery);

        TomestoneManager = new TomestoneManager(DataManager);
        GearManager = new GearManager(DataManager, ObjectTable);
        RaidManager = new RaidManager(DataManager);
        MiscellanyManager = new MiscellanyManager(DataManager, Log);
        BiSManager = new BiSManager(this, DataManager, Log);
        StatCalculator = new StatCalculator(DataManager, Log);
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

        if (ClientState.IsLoggedIn)
        {
            Framework.Update += OnFrameworkUpdate;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
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
        2,  // PGL
        3,  // MRD
        4,  // LNC
        5,  // ARC
        6,  // CNJ
        7,  // THM
        26, // ACN
        29, // ROG
        36, //BLU
        
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

            if (!Configuration.CharacterOrder.Contains(CurrentContentId))
                Configuration.CharacterOrder.Add(CurrentContentId);
        }
        
        var charData = Configuration.Characters[CurrentContentId];
        charData.Tomestones = TomestoneManager.UpdateTomestones();
        
        foreach (var id in ExcludedJobs)
        {
            charData.GearSets.Remove(id);
            charData.BisSets.Remove(id);
        }
        
        charData.RaidProgress = RaidManager.GetRaidDataFromLockoutInfo();
        charData.Miscellany = MiscellanyManager.ScanInventories();
        
        Configuration.Save();
    }

    /// <summary>
    /// Manually captures the currently equipped gearset and adds it to the
    /// specified character's tracked gear sets. Called when the user clicks "+".
    /// If no characterId is provided, defaults to the currently logged-in character.
    /// </summary>
    public void TrackCurrentGearset(ulong? characterId = null)
    {
        var targetId = characterId ?? CurrentContentId;
        if (targetId == 0) return;
        if (!Configuration.Characters.TryGetValue(targetId, out var charData)) return;

        var gear = GearManager.UpdateGear();
        if (gear.JobId == 0) return;

        if (ExcludedJobs.Contains(gear.JobId))
        {
            Log.Information($"Skipping track for excluded job {gear.JobId}");
            return;
        }

        charData.GearSets[gear.JobId] = gear;
        Configuration.Save();
        Log.Information("Tracked gearset for job {JobId} (IL {IL}) for character {CharId}", gear.JobId, gear.AverageItemLevel, targetId);
    }

    public void RunOnFramework(Action action)
    {
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

    public void CheckAndTrackGear(uint jobId, ulong? characterId = null)
    {
        var targetId = characterId ?? CurrentContentId;
        RunOnFramework(() =>
        {
            if (targetId == 0) return;
            if (!Configuration.Characters.TryGetValue(targetId, out var charData)) return;

            if (charData.GearSets.ContainsKey(jobId)) return;
            if (targetId != CurrentContentId) return;

            if (Plugin.ObjectTable.Length > 0 && Plugin.ObjectTable[0] is IPlayerCharacter player && player.ClassJob.RowId == jobId)
            {
                Log.Information("Auto-tracking gear for imported BiS job {JobId}", jobId);
                TrackCurrentGearset(targetId);
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
        MainWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
