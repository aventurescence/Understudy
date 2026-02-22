using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using Understudy.Models;

namespace Understudy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool ShowInDuty { get; set; } = false;
    public bool ShowJobCategoryInDashboard { get; set; } = true;
    public bool CompactMode { get; set; } = false;
    public bool VerboseLogging { get; set; } = false;
    public bool ReorderUnlocked { get; set; } = false;

    public Dictionary<ulong, CharacterData> Characters { get; set; } = new();
    public List<ulong> CharacterOrder { get; set; } = new();

    public uint DashboardFrameImageId { get; set; } = 0;
    public float DashboardFrameOpacity { get; set; } = 0.6f;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
