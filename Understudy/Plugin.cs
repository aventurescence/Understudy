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
using Understudy.Managers;
using Understudy.Models;
using Understudy.Windows;

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
    public BiSImportManager BiSImportManager { get; init; }
    public BiSComparisonManager BiSComparisonManager { get; init; }
    public EtroBrowseManager EtroBrowseManager { get; init; }
    public StatCalculator StatCalculator { get; init; }
    public MateriaTextureManager MateriaTextures { get; init; }
    public CharacterTracker CharacterTracker { get; init; }

    internal readonly HttpClient HttpClient = new();

    public readonly WindowSystem WindowSystem = new("Understudy");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

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
        BiSImportManager = new BiSImportManager(this, DataManager, Log);
        BiSComparisonManager = new BiSComparisonManager(this, DataManager, Log);
        EtroBrowseManager = new EtroBrowseManager(this, DataManager, Log);
        StatCalculator = new StatCalculator(DataManager, Log);
        MateriaTextures = new MateriaTextureManager(this);
        CharacterTracker = new CharacterTracker(this, ClientState, ObjectTable, PlayerState, Framework, Log);

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

        Log.Information("Understudy initialized.");
    }


    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        
        CharacterTracker.Dispose();
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
