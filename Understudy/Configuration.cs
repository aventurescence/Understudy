using Dalamud.Configuration;
using System;
using System.Collections.Generic;

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

    public Dictionary<ulong, CharacterData> Characters { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
