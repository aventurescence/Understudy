using System;
using System.Collections.Generic;
using Understudy.Models;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Understudy.Managers;

public class CharacterTracker : IDisposable
{
    private readonly Plugin plugin;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly IPluginLog log;

    public ulong CurrentContentId { get; private set; }

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
        36, // BLU
        
        // DoH
        8, 9, 10, 11, 12, 13, 14, 15,
        
        // DoL
        16, 17, 18
    };

    public CharacterTracker(Plugin plugin, IClientState clientState, IObjectTable objectTable, IPlayerState playerState, IFramework framework, IPluginLog log)
    {
        this.plugin = plugin;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.playerState = playerState;
        this.framework = framework;
        this.log = log;

        this.clientState.Login += OnLogin;
        this.clientState.Logout += OnLogout;

        if (this.clientState.IsLoggedIn)
        {
            this.framework.Update += OnFrameworkUpdate;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        this.framework.Update -= OnFrameworkUpdate;
        try 
        {
             OnLogin();
        }
        catch (Exception ex)
        {
             log.Error(ex, "Failed to initialize data on startup via Framework.Update.");
        }
    }

    private void OnLogin()
    {
        CurrentContentId = playerState.ContentId;
        UpdateCharacterData();
    }

    private void OnLogout(int type, int code)
    {
        CurrentContentId = 0;
    }

    public bool IsJobExcluded(uint jobId) => ExcludedJobs.Contains(jobId);

    public void UpdateCharacterData()
    {
        if (CurrentContentId == 0) return;
        
        if (!plugin.Configuration.Characters.ContainsKey(CurrentContentId))
        {
            var p = objectTable[0] as IPlayerCharacter;
            plugin.Configuration.Characters[CurrentContentId] = new CharacterData
            {
                Name = p?.Name.ToString() ?? "Unknown",
                WorldId = p?.HomeWorld.RowId ?? 0,
                ContentId = CurrentContentId
            };

            if (!plugin.Configuration.CharacterOrder.Contains(CurrentContentId))
                plugin.Configuration.CharacterOrder.Add(CurrentContentId);
        }
        
        var charData = plugin.Configuration.Characters[CurrentContentId];
        charData.Tomestones = plugin.TomestoneManager.UpdateTomestones();
        
        foreach (var id in ExcludedJobs)
        {
            charData.GearSets.Remove(id);
            charData.BisSets.Remove(id);
        }
        
        charData.RaidProgress = plugin.RaidManager.GetRaidDataFromLockoutInfo();
        charData.Miscellany = plugin.MiscellanyManager.ScanInventories();
        
        plugin.Configuration.Save();
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
        if (!plugin.Configuration.Characters.TryGetValue(targetId, out var charData)) return;

        var gear = plugin.GearManager.UpdateGear();
        if (gear.JobId == 0) return;

        if (ExcludedJobs.Contains(gear.JobId))
        {
            log.Information($"Skipping track for excluded job {gear.JobId}");
            return;
        }

        charData.GearSets[gear.JobId] = gear;
        plugin.Configuration.Save();
        log.Information("Tracked gearset for job {JobId} (IL {IL}) for character {CharId}", gear.JobId, gear.AverageItemLevel, targetId);
    }

    public void RunOnFramework(Action action)
    {
        var handler = new FrameworkHandler(action, framework);
        framework.Update += handler.OnUpdate;
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
            if (!plugin.Configuration.Characters.TryGetValue(targetId, out var charData)) return;

            if (charData.GearSets.ContainsKey(jobId)) return;
            if (targetId != CurrentContentId) return;

            if (objectTable.Length > 0 && objectTable[0] is IPlayerCharacter player && player.ClassJob.RowId == jobId)
            {
                log.Information("Auto-tracking gear for imported BiS job {JobId}", jobId);
                TrackCurrentGearset(targetId);
            }
        });
    }

    public void Dispose()
    {
        clientState.Login -= OnLogin;
        clientState.Logout -= OnLogout;
    }
}
