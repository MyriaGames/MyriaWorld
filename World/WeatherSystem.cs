using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaWorld.UI;

namespace MyriaWorld.World;

public enum WeatherType { Clear, Cloudy, LightRain, HeavyRain, Fog, Storm }

/// <summary>
/// Manages weather state transitions and provides fog parameters for BasicEffect
/// and a 2-D rain/overlay pass for SpriteBatch.
/// </summary>
public sealed class WeatherSystem
{
    // ── state machine ─────────────────────────────────────────────────────────

    private WeatherType _from    = WeatherType.Clear;
    public  WeatherType Current  { get; private set; } = WeatherType.Clear;
    private float       _blend   = 1f;   // 0 = fully _from, 1 = fully Current
    private float       _holdTimer;
    private readonly Random _rng = new(1337);

    // ── 3-D fog (set on BasicEffect before each draw) ─────────────────────────

    public float   FogStart { get; private set; } = 400f;
    public float   FogEnd   { get; private set; } = 800f;
    public Vector3 FogColor { get; private set; } = new(0.7f, 0.75f, 0.80f);
    public Color   SkyColor { get; private set; } = new Color(85, 115, 145);

    // ── rain particles ────────────────────────────────────────────────────────

    private const int MaxDrops = 700;
    private readonly float[] _dropX     = new float[MaxDrops];
    private readonly float[] _dropY     = new float[MaxDrops];
    private readonly float[] _dropSpeed = new float[MaxDrops];

    public string DisplayName => Current switch
    {
        WeatherType.Clear     => "",
        WeatherType.Cloudy    => "Cloudy",
        WeatherType.LightRain => "Rain",
        WeatherType.HeavyRain => "Heavy Rain",
        WeatherType.Fog       => "Foggy",
        WeatherType.Storm     => "Storm",
        _                     => ""
    };

    public WeatherSystem()
    {
        ResetHold();
        // Scatter drops across a 1920×1080 virtual canvas; resized at draw time
        for (int i = 0; i < MaxDrops; i++)
            ResetDrop(i, 1920, 1080, scatter: true);
    }

    // ── update ────────────────────────────────────────────────────────────────

    public void Update(float dt, int sw, int sh)
    {
        // Transition blend
        _blend = MathHelper.Clamp(_blend + dt / 10f, 0f, 1f);

        // Hold timer — pick next state when expired
        _holdTimer -= dt;
        if (_holdTimer <= 0f)
        {
            _from  = Current;
            Current = PickNext(Current);
            _blend  = 0f;
            ResetHold();
        }

        // Interpolated fog & sky
        var (fs0, fe0, fc0, sky0) = Params(_from);
        var (fs1, fe1, fc1, sky1) = Params(Current);
        FogStart = MathHelper.Lerp(fs0, fs1, _blend);
        FogEnd   = MathHelper.Lerp(fe0, fe1, _blend);
        FogColor = Vector3.Lerp(fc0, fc1, _blend);
        SkyColor = Color.Lerp(sky0, sky1, _blend);

        // Rain drop movement
        int   active = ActiveDropCount();
        float windX  = WindX();
        for (int i = 0; i < active; i++)
        {
            _dropY[i] += _dropSpeed[i] * dt;
            _dropX[i] += windX * dt;
            if (_dropY[i] > sh + 20 || _dropX[i] < -10 || _dropX[i] > sw + 10)
                ResetDrop(i, sw, sh, scatter: false);
        }
    }

    // ── 2-D overlay (call inside SpriteBatch.Begin / End) ────────────────────

    public void DrawOverlay(SpriteBatch sb, int sw, int sh)
    {
        // Atmospheric tint (darkening + hue for storm/fog)
        Color tint = AtmosTint();
        if (tint.A > 0)
            Gfx.Rect(sb, 0, 0, sw, sh, tint);

        // Rain drops
        int count    = ActiveDropCount();
        int dropH    = DropH();
        int dropW    = DropW();
        Color dropC  = DropColor();
        for (int i = 0; i < count; i++)
            Gfx.Rect(sb, (int)_dropX[i], (int)_dropY[i], dropW, dropH, dropC);
    }

    // ── fog → BasicEffect ─────────────────────────────────────────────────────

    public void ApplyFog(BasicEffect effect)
    {
        effect.FogEnabled = true;
        effect.FogStart   = FogStart;
        effect.FogEnd     = FogEnd;
        effect.FogColor   = FogColor;
    }

    // ── weather parameter tables ──────────────────────────────────────────────

    private static (float start, float end, Vector3 color, Color sky) Params(WeatherType w) => w switch
    {
        WeatherType.Clear     => (450f, 900f, new(0.70f, 0.75f, 0.80f), new Color( 85, 115, 145)),
        WeatherType.Cloudy    => (280f, 650f, new(0.62f, 0.66f, 0.72f), new Color( 68,  90, 112)),
        WeatherType.LightRain => (130f, 340f, new(0.52f, 0.57f, 0.65f), new Color( 55,  72,  92)),
        WeatherType.HeavyRain => ( 60f, 180f, new(0.38f, 0.42f, 0.50f), new Color( 42,  54,  68)),
        WeatherType.Fog       => ( 20f,  90f, new(0.80f, 0.82f, 0.84f), new Color(145, 152, 158)),
        WeatherType.Storm     => ( 40f, 130f, new(0.28f, 0.30f, 0.36f), new Color( 35,  42,  52)),
        _                     => (350f, 700f, new(0.70f, 0.75f, 0.80f), new Color( 85, 115, 145)),
    };

    private Color AtmosTint() => Current switch
    {
        WeatherType.Cloudy    => new Color( 15,  20,  35,  30),
        WeatherType.LightRain => new Color( 15,  25,  45,  55),
        WeatherType.HeavyRain => new Color( 10,  18,  38,  95),
        WeatherType.Fog       => new Color(175, 180, 185,  65),
        WeatherType.Storm     => new Color(  8,  12,  28, 135),
        _                     => Color.Transparent,
    };

    private int ActiveDropCount() => Current switch
    {
        WeatherType.LightRain => MaxDrops / 4,
        WeatherType.HeavyRain => MaxDrops * 2 / 3,
        WeatherType.Storm     => MaxDrops,
        _                     => 0,
    };

    private float WindX() => Current switch
    {
        WeatherType.LightRain => 25f,
        WeatherType.HeavyRain => 55f,
        WeatherType.Storm     => 115f,
        _                     => 0f,
    };

    private static int DropH() => 8;
    private static int DropW() => 1;

    private Color DropColor() => Current switch
    {
        WeatherType.Storm     => new Color(185, 200, 225, 165),
        WeatherType.HeavyRain => new Color(195, 210, 235, 145),
        _                     => new Color(205, 220, 245, 105),
    };

    // ── helpers ───────────────────────────────────────────────────────────────

    private void ResetDrop(int i, int sw, int sh, bool scatter)
    {
        _dropX[i]     = (float)(_rng.NextDouble() * sw);
        _dropY[i]     = scatter ? (float)(_rng.NextDouble() * sh) : -20f;
        _dropSpeed[i] = 300f + (float)(_rng.NextDouble() * 200f);
    }

    private void ResetHold()
    {
        _holdTimer = Current switch
        {
            WeatherType.Clear     => 150f + (float)_rng.NextDouble() * 150f,
            WeatherType.Fog       => 100f + (float)_rng.NextDouble() * 120f,
            WeatherType.Storm     => 25f  + (float)_rng.NextDouble() * 40f,
            _                     => 55f  + (float)_rng.NextDouble() * 75f,
        };
    }

    private WeatherType PickNext(WeatherType from)
    {
        float r = (float)_rng.NextDouble();
        return from switch
        {
            WeatherType.Clear     => r < 0.60f ? WeatherType.Clear
                                   : r < 0.82f ? WeatherType.Cloudy
                                   :              WeatherType.Fog,

            WeatherType.Cloudy    => r < 0.22f ? WeatherType.Clear
                                   : r < 0.50f ? WeatherType.Cloudy
                                   : r < 0.78f ? WeatherType.LightRain
                                   :              WeatherType.Fog,

            WeatherType.LightRain => r < 0.22f ? WeatherType.Cloudy
                                   : r < 0.50f ? WeatherType.LightRain
                                   : r < 0.80f ? WeatherType.HeavyRain
                                   :              WeatherType.Storm,

            WeatherType.HeavyRain => r < 0.28f ? WeatherType.LightRain
                                   : r < 0.58f ? WeatherType.HeavyRain
                                   : r < 0.80f ? WeatherType.Storm
                                   :              WeatherType.Cloudy,

            WeatherType.Storm     => r < 0.50f ? WeatherType.HeavyRain
                                   : r < 0.80f ? WeatherType.Storm
                                   :              WeatherType.LightRain,

            WeatherType.Fog       => r < 0.42f ? WeatherType.Clear
                                   : r < 0.72f ? WeatherType.Fog
                                   :              WeatherType.Cloudy,

            _                     => WeatherType.Clear,
        };
    }
}
