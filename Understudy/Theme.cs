using System;
using System.Numerics;

namespace Understudy;

/// <summary>
/// Color palette and centralized style constants for the plugin.
/// Inspired by dark-mode analytics dashboards with vibrant accents.
/// </summary>
public static class Theme
{
    // ── Backgrounds ──────────────────────────────────────────────
    public static readonly Vector4 BgDark        = new(0.06f, 0.07f, 0.09f, 1.0f);
    public static readonly Vector4 BgCard        = new(0.10f, 0.11f, 0.15f, 0.95f);
    public static readonly Vector4 BgCardHover   = new(0.13f, 0.15f, 0.23f, 1.0f);
    public static readonly Vector4 BgSidebar     = new(0.08f, 0.09f, 0.13f, 1.0f);
    public static readonly Vector4 BgTableRow    = new(0.09f, 0.10f, 0.13f, 0.6f);
    public static readonly Vector4 BgTableRowAlt = new(0.11f, 0.12f, 0.16f, 0.6f);

    // ── Borders ──────────────────────────────────────────────────
    public static readonly Vector4 BorderSubtle  = new(0.16f, 0.18f, 0.24f, 1.0f);
    public static readonly Vector4 BorderCard    = new(0.23f, 0.25f, 0.33f, 0.6f);

    // ── Accents ──────────────────────────────────────────────────
    public static readonly Vector4 AccentPrimary   = new(0.49f, 0.42f, 1.0f, 1.0f);   // Vibrant violet
    public static readonly Vector4 AccentSecondary = new(0.38f, 0.65f, 0.98f, 1.0f);  // Soft blue
    public static readonly Vector4 AccentSuccess   = new(0.20f, 0.83f, 0.60f, 1.0f);  // Emerald green
    public static readonly Vector4 AccentWarning   = new(0.98f, 0.75f, 0.14f, 1.0f);  // Gold/amber
    public static readonly Vector4 AccentDanger    = new(0.97f, 0.44f, 0.44f, 1.0f);  // Soft red

    // ── Text ─────────────────────────────────────────────────────
    public static readonly Vector4 TextPrimary   = new(0.89f, 0.91f, 0.94f, 1.0f);
    public static readonly Vector4 TextSecondary = new(0.58f, 0.64f, 0.72f, 1.0f);
    public static readonly Vector4 TextDisabled  = new(0.28f, 0.33f, 0.41f, 1.0f);

    // ── Layout Constants ─────────────────────────────────────────
    public const float SidebarWidth  = 190.0f;
    public const float CardRounding  = 8.0f;
    public const float CardPadding   = 12.0f;
    public const float BarHeight     = 20.0f;
    public const float SectionSpace  = 16.0f;
    public const float ItemSpacing   = 6.0f;

    // ── Glow / overlay helpers ────────────────────────────────────
    public static readonly Vector4 GlowSuccess   = new(0.20f, 0.83f, 0.60f, 0.25f);
    public static readonly Vector4 GlowPrimary   = new(0.49f, 0.42f, 1.0f, 0.18f);
    public static readonly Vector4 OverlayDark   = new(0.0f, 0.0f, 0.0f, 0.45f);

    // ── BiS Comparison ───────────────────────────────────────────
    public static readonly Vector4 BiSOwned    = new(0.20f, 0.83f, 0.60f, 1.0f);  // Green
    public static readonly Vector4 BiSMissing  = new(0.97f, 0.44f, 0.44f, 1.0f);  // Red
    public static readonly Vector4 BiSAlert    = new(1.0f, 0.85f, 0.0f, 1.0f);    // Gold
    public static readonly Vector4 BgPopup     = new(0.12f, 0.13f, 0.18f, 0.98f);

    // ── Gearset Card ─────────────────────────────────────────────
    public static readonly Vector4 GearHeaderBar   = new(0.12f, 0.13f, 0.19f, 1.0f);   // Slightly lighter bar
    public static readonly Vector4 GearHeaderHover = new(0.15f, 0.17f, 0.25f, 1.0f);   // Hover state for header
    public static readonly Vector4 JobBadgeBg      = new(0.49f, 0.42f, 1.0f, 0.15f);   // Transparent violet
    public static readonly Vector4 TrackBtnBg      = new(0.20f, 0.83f, 0.60f, 0.20f);  // Translucent green
    public static readonly Vector4 TrackBtnHover   = new(0.20f, 0.83f, 0.60f, 0.45f);  // Hover green
    public static readonly Vector4 ILTierMax       = new(0.80f, 0.65f, 1.0f, 1.0f);    // Purple for max IL
    public const float GearCardRounding = 6.0f;
    public const float GearHeaderHeight = 36.0f;

    // ── Item Level Tier Thresholds ─────────────────────────────
    public const float ILThresholdMax  = 790f;  // Max IL (purple)
    public const float ILThresholdHigh = 780f;  // High IL (green)
    public const float ILThresholdMid  = 770f;  // Mid IL (gold/warning)

    // ── Popup / Import UI ───────────────────────────────────────
    public static readonly Vector4 EtroRowBg         = new(0.10f, 0.11f, 0.16f, 0.70f);
    public static readonly Vector4 EtroRowHover      = new(0.16f, 0.17f, 0.26f, 0.90f);
    public static readonly Vector4 EtroRowSelected   = new(0.49f, 0.42f, 1.0f, 0.12f);
    public static readonly Vector4 InputFieldBg      = new(0.07f, 0.08f, 0.11f, 1.0f);
    public static readonly Vector4 InputFieldBorder  = new(0.22f, 0.24f, 0.32f, 0.80f);
    public static readonly Vector4 SlotBtnBg         = new(0.12f, 0.13f, 0.19f, 1.0f);
    public static readonly Vector4 SlotBtnHover      = new(0.18f, 0.20f, 0.30f, 1.0f);
    public static readonly Vector4 SectionHeaderBg   = new(0.08f, 0.09f, 0.13f, 0.60f);
    public static readonly Vector4 TabActiveBg       = new(0.49f, 0.42f, 1.0f, 0.20f);
    public static readonly Vector4 ProviderEtro      = new(0.38f, 0.78f, 0.95f, 1.0f);   // Etro cyan
    public static readonly Vector4 ProviderXIVGear   = new(0.95f, 0.65f, 0.30f, 1.0f);   // XIVGear amber
    public const float PopupRounding = 12.0f;
    public const float RowRounding   = 6.0f;
    public const float SlotBtnSize   = 48.0f;

    // ── Combined Loadout Card ────────────────────────────────────
    public static readonly Vector4 BiSProgressBg     = new(0.10f, 0.11f, 0.16f, 1.0f);   // Dark track
    public static readonly Vector4 BiSProgressFill   = new(0.30f, 0.70f, 0.55f, 1.0f);   // Teal-green fill
    public static readonly Vector4 SlotMatchBg       = new(0.15f, 0.30f, 0.22f, 0.25f);  // Subtle green row
    public static readonly Vector4 SlotMissingBg     = new(0.30f, 0.14f, 0.14f, 0.18f);  // Subtle red row
    public static readonly Vector4 ArrowColor        = new(0.49f, 0.42f, 1.0f, 0.55f);   // Muted violet arrow
    public static readonly Vector4 CostPillBg        = new(0.14f, 0.16f, 0.22f, 0.90f);  // Cost pill background
    public static readonly Vector4 LinkAction        = new(0.55f, 0.70f, 1.0f, 0.90f);   // Clickable link blue

    /// <summary>
    /// Linearly interpolates between two colors according to the ratio [0,1].
    /// Useful for progress bars that change color gradually.
    /// </summary>
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Vector4(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t);
    }

    /// <summary>
    /// Returns a progress bar color according to the ratio:
    ///   0.0 -> Danger (red)
    ///   0.5 -> Warning (amber)
    ///   1.0 -> Success (green)
    /// </summary>
    public static Vector4 ProgressColor(float ratio)
    {
        if (ratio < 0.5f)
            return Lerp(AccentDanger, AccentWarning, ratio * 2f);
        return Lerp(AccentWarning, AccentSuccess, (ratio - 0.5f) * 2f);
    }
}
