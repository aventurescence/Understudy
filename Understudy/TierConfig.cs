namespace Understudy;

/// <summary>
/// Centralizes all tier-specific constants (raid names, gear prefixes, item keywords).
/// When a new raid tier releases, update this single file instead of hunting through managers.
/// </summary>
public static class TierConfig
{
    // ── Raid Tier ──────────────────────────────────────────────────
    public static readonly string[] RaidNames = new[]
    {
        "AAC Heavyweight M1 (Savage)",
        "AAC Heavyweight M2 (Savage)",
        "AAC Heavyweight M3 (Savage)",
        "AAC Heavyweight M4 (Savage)",
    };

    // ── Gear Source Classification (item name prefixes) ────────────
    public const string SavageGearPrefix = "Grand Champion";
    public const string AugmentedTomeGearPrefix = "Augmented Bygone";
    public const string BaseTomeGearPrefix = "Bygone";

    // ── Miscellany Item Keywords ──────────────────────────────────
    public const string BookKeyword = "AAC Illustrated";
    public const string MaterialKeyword = "Thunderstee";
    public const string CofferKeyword = "Grand Champion";
    public const string UniversalTomestoneKeyword = "Universal Tomestone";

    // ── Display ───────────────────────────────────────────────────
    public const string CofferDisplayTrim = "Grand Champion's ";
}
