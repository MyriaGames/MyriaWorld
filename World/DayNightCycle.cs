using Microsoft.Xna.Framework;

namespace Myria.Mono.World;

/// <summary>
/// Manages an in-game day/night cycle.
/// TimeOfDay is in [0, 1): 0.0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk.
/// One full day lasts <see cref="SecondsPerDay"/> real seconds.
/// </summary>
public sealed class DayNightCycle
{
    public const float SecondsPerDay = 720f;  // 12 real minutes per game day

    /// <summary>Fraction of the day elapsed — 0 = midnight, 0.5 = noon.</summary>
    public float TimeOfDay { get; private set; } = 0.35f;  // start near morning

    /// <summary>True between dusk (0.78) and dawn (0.22) — outdoor areas become dangerous.</summary>
    public bool IsNight => TimeOfDay < 0.22f || TimeOfDay > 0.78f;

    public void Update(float dt)
        => TimeOfDay = (TimeOfDay + dt / SecondsPerDay) % 1f;

    /// <summary>
    /// Fullscreen RGBA overlay to draw over the 3D world (before the HUD).
    /// Transparent at noon; dark blue at midnight with orange flares at dawn/dusk.
    /// </summary>
    public Color GetOverlayColor()
    {
        float t = TimeOfDay;

        // ── night (0.00–0.20): dark blue ──────────────────────────────────────
        if (t < 0.20f)
        {
            float f = t / 0.20f;
            return Lerp4(10, 10, 55, 175, 18, 14, 45, 120, f);
        }
        // ── pre-dawn (0.20–0.25): blue→orange flash ───────────────────────────
        if (t < 0.25f)
        {
            float f = (t - 0.20f) / 0.05f;
            return Lerp4(18, 14, 45, 120, 190, 85, 20, 60, f);
        }
        // ── dawn (0.25–0.35): orange fades out ────────────────────────────────
        if (t < 0.35f)
        {
            float f = (t - 0.25f) / 0.10f;
            return Lerp4(190, 85, 20, 60, 0, 0, 0, 0, f);
        }
        // ── day (0.35–0.65): transparent ─────────────────────────────────────
        if (t < 0.65f)
            return Color.Transparent;
        // ── dusk (0.65–0.75): orange wash fades in ────────────────────────────
        if (t < 0.75f)
        {
            float f = (t - 0.65f) / 0.10f;
            return Lerp4(0, 0, 0, 0, 200, 65, 10, 55, f);
        }
        // ── post-dusk (0.75–0.80): orange→indigo ─────────────────────────────
        if (t < 0.80f)
        {
            float f = (t - 0.75f) / 0.05f;
            return Lerp4(200, 65, 10, 55, 18, 14, 45, 100, f);
        }
        // ── night (0.80–1.00): indigo→midnight blue ───────────────────────────
        {
            float f = (t - 0.80f) / 0.20f;
            return Lerp4(18, 14, 45, 100, 10, 10, 55, 175, f);
        }
    }

    /// <summary>"06:45" format for the HUD clock.</summary>
    public string FormatTime()
    {
        int total = (int)(TimeOfDay * 1440);  // minutes in a day
        return $"{total / 60:D2}:{total % 60:D2}";
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Color Lerp4(
        int r0, int g0, int b0, int a0,
        int r1, int g1, int b1, int a1, float t)
        => new(
            (int)MathHelper.Lerp(r0, r1, t),
            (int)MathHelper.Lerp(g0, g1, t),
            (int)MathHelper.Lerp(b0, b1, t),
            (int)MathHelper.Lerp(a0, a1, t));
}
