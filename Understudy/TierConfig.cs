using Understudy.Managers;

namespace Understudy;

/// <summary>
/// Provides tier-specific data discovered automatically from SpecialShop at startup.
/// No manual updates needed when a new raid tier releases.
/// </summary>
public static class TierConfig
{
    private static TierDiscovery? discovery;

    /// <summary>Called once at startup to bind the discovery data.</summary>
    public static void Initialize(TierDiscovery tierDiscovery) => discovery = tierDiscovery;

    // ── Raid Tier ──────────────────────────────────────────────────
    public static string[] RaidNames => discovery?.RaidNames ?? [];

    // ── Gear Source Classification (item name prefixes) ────────────
    public static string SavageGearPrefix => discovery?.SavageGearPrefix ?? string.Empty;
    public static string AugmentedTomeGearPrefix => discovery?.AugmentedTomeGearPrefix ?? string.Empty;
    public static string BaseTomeGearPrefix => discovery?.BaseTomeGearPrefix ?? string.Empty;

    // ── Miscellany Item Keywords ──────────────────────────────────
    public static string BookKeyword => discovery?.BookKeyword ?? string.Empty;
    public static string MaterialKeyword => discovery?.MaterialKeyword ?? string.Empty;
    public static string CofferKeyword => discovery?.CofferKeyword ?? string.Empty;
    public static string UniversalTomestoneKeyword => discovery?.UniversalTomestoneKeyword ?? string.Empty;

    // ── Display ───────────────────────────────────────────────────
    public static string CofferDisplayTrim => discovery?.CofferDisplayTrim ?? string.Empty;

    // ── Food Filtering ──────────────────────────────────────────
    public const uint FoodCategoryId = 46;       // ItemUICategory: Meal (stable across tiers)
    public static uint FoodMinItemLevel => discovery?.FoodMinItemLevel ?? 690;
}
