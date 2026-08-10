using Microsoft.Xna.Framework.Audio;

namespace Myria.Mono.Services;

/// <summary>
/// Generates all game sounds procedurally from PCM data (no external audio files required).
/// Call Init() once on the main thread after MonoGame starts, then Play() / SetAmbientZone() anywhere.
/// </summary>
public static class AudioService
{
    private const int Rate = 22050; // Hz, mono 16-bit

    public enum Sfx
    {
        Footstep, Hit, SkillCast, MonsterDeath,
        LevelUp, QuestAccept, QuestComplete, PickUp,
        EnterDungeon, OpenMenu
    }

    private static readonly Dictionary<Sfx, SoundEffect>    _sfx       = new();
    private static readonly Dictionary<string, SoundEffect>  _ambientSfx = new();
    private static SoundEffectInstance? _ambientInst;
    private static string               _ambientZone = "";
    private static bool                 _ready;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public static void Init()
    {
        try
        {
            _sfx[Sfx.Footstep]    = Sfx_(Footstep());
            _sfx[Sfx.Hit]         = Sfx_(Hit());
            _sfx[Sfx.SkillCast]   = Sfx_(SkillCast());
            _sfx[Sfx.MonsterDeath]= Sfx_(Death());
            _sfx[Sfx.LevelUp]     = Sfx_(LevelUp());
            _sfx[Sfx.QuestAccept] = Sfx_(QuestAccept());
            _sfx[Sfx.QuestComplete]= Sfx_(QuestComplete());
            _sfx[Sfx.PickUp]      = Sfx_(PickUp());
            _sfx[Sfx.EnterDungeon]= Sfx_(EnterDungeon());
            _sfx[Sfx.OpenMenu]    = Sfx_(MenuOpen());

            _ambientSfx["grass"]   = Sfx_(AmbientOutdoor());
            _ambientSfx["forest"]  = Sfx_(AmbientForest());
            _ambientSfx["dungeon"] = Sfx_(AmbientDungeon());
            _ambientSfx["cave"]    = Sfx_(AmbientDungeon());
            _ambientSfx["city"]    = Sfx_(AmbientOutdoor());

            _ready = true;
        }
        catch { /* audio init may fail on some platforms — degrade gracefully */ }
    }

    public static void Play(Sfx id, float volume = 1f, float pitch = 0f)
    {
        if (!_ready) return;
        if (_sfx.TryGetValue(id, out var sfx))
            sfx.Play(Math.Min(volume, 1f), Math.Clamp(pitch, -1f, 1f), 0f);
    }

    public static void SetAmbientZone(string terrain)
    {
        if (!_ready || terrain == _ambientZone) return;
        _ambientZone = terrain;

        _ambientInst?.Stop(immediate: true);
        _ambientInst?.Dispose();
        _ambientInst = null;

        string key = terrain.ToLowerInvariant();
        if (!_ambientSfx.ContainsKey(key)) key = "grass";
        var sfx = _ambientSfx[key];
        _ambientInst = sfx.CreateInstance();
        _ambientInst.IsLooped = true;
        _ambientInst.Volume   = 0.12f;
        _ambientInst.Play();
    }

    public static void Dispose()
    {
        _ambientInst?.Stop(); _ambientInst?.Dispose();
        foreach (var s in _sfx.Values)     s.Dispose();
        foreach (var s in _ambientSfx.Values) s.Dispose();
        _sfx.Clear(); _ambientSfx.Clear();
        _ready = false;
    }

    // ── PCM helpers ───────────────────────────────────────────────────────────

    private static SoundEffect Sfx_(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            bytes[i * 2]     = (byte)(samples[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }
        return new SoundEffect(bytes, Rate, AudioChannels.Mono);
    }

    private static short S(float f) => (short)Math.Clamp((int)(f * 32767), short.MinValue, short.MaxValue);

    private static short[] Sine(float freq, float dur, float vol = 0.5f, bool fadeOut = true)
    {
        int n = (int)(Rate * dur);
        var b = new short[n];
        for (int i = 0; i < n; i++)
        {
            float t   = i / (float)Rate;
            float env = fadeOut ? 1f - i / (float)n : 1f;
            b[i] = S(MathF.Sin(MathF.Tau * freq * t) * vol * env);
        }
        return b;
    }

    private static short[] Noise(float dur, float vol = 0.3f, float decay = 2f)
    {
        int n = (int)(Rate * dur);
        var b = new short[n];
        var r = new Random(42);
        for (int i = 0; i < n; i++)
        {
            float env = MathF.Exp(-decay * i / n);
            b[i] = S((r.NextSingle() * 2f - 1f) * vol * env);
        }
        return b;
    }

    private static short[] Sweep(float f0, float f1, float dur, float vol = 0.45f)
    {
        int n = (int)(Rate * dur);
        var b = new short[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float frac  = i / (float)n;
            float freq  = f0 + (f1 - f0) * frac;
            float env   = frac < 0.3f ? frac / 0.3f : 1f - (frac - 0.3f) / 0.7f;
            phase += MathF.Tau * freq / Rate;
            b[i] = S(MathF.Sin(phase) * vol * env);
        }
        return b;
    }

    private static short[] Mix(params short[][] tracks)
    {
        int len = tracks.Max(t => t.Length);
        var out_ = new short[len];
        foreach (var t in tracks)
            for (int i = 0; i < t.Length; i++)
                out_[i] = S(out_[i] / 32767f + t[i] / 32767f);
        return out_;
    }

    private static short[] Cat(params short[][] parts)
    {
        var buf = new short[parts.Sum(p => p.Length)];
        int pos = 0;
        foreach (var p in parts) { Array.Copy(p, 0, buf, pos, p.Length); pos += p.Length; }
        return buf;
    }

    // ── Sound generators ──────────────────────────────────────────────────────

    private static short[] Footstep()
    {
        // short click: noise burst with very fast decay
        int n = (int)(Rate * 0.035f);
        var b = new short[n];
        var r = new Random(7);
        for (int i = 0; i < n; i++)
        {
            float env = MathF.Exp(-18f * i / n);
            // low-pass by mixing with previous
            b[i] = S((r.NextSingle() * 2f - 1f) * 0.4f * env);
            if (i > 0) b[i] = (short)((b[i] + b[i - 1]) / 2);
        }
        return b;
    }

    private static short[] Hit()
        => Mix(Noise(0.07f, 0.55f, 5f), Sine(200f, 0.07f, 0.2f));

    private static short[] SkillCast()
        => Mix(Sweep(350f, 1100f, 0.15f), Noise(0.05f, 0.15f, 3f));

    private static short[] Death()
        => Mix(Sweep(350f, 80f, 0.28f, 0.4f), Noise(0.28f, 0.25f, 1.5f));

    private static short[] LevelUp()
    {
        // Ascending 4-note arpeggio: C4, E4, G4, C5
        float[] notes = [261.6f, 329.6f, 392f, 523.3f];
        return Cat(notes.Select(f => Sine(f, 0.12f, 0.45f)).ToArray());
    }

    private static short[] QuestAccept()
        => Cat(Sine(440f, 0.08f, 0.35f), Sine(550f, 0.12f, 0.35f));

    private static short[] QuestComplete()
        => Cat(Sine(440f, 0.08f, 0.4f), Sine(550f, 0.08f, 0.4f), Sine(660f, 0.18f, 0.4f));

    private static short[] PickUp()
        => Sine(1400f, 0.07f, 0.3f);

    private static short[] EnterDungeon()
        => Mix(Sine(65f, 0.55f, 0.35f, fadeOut: false), Sine(78f, 0.55f, 0.2f, fadeOut: false),
               Noise(0.55f, 0.08f, 0.5f));

    private static short[] MenuOpen()
        => Sweep(600f, 900f, 0.08f, 0.3f);

    // ── Ambient generators (looped) ───────────────────────────────────────────

    private static short[] AmbientOutdoor()
    {
        // Gentle wind: heavy low-pass on white noise
        int n = (int)(Rate * 3.5f);
        var b = new short[n];
        var r = new Random(101);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (r.NextSingle() * 2f - 1f) * 0.06f;
            prev  = prev * 0.985f + white * 0.015f;
            b[i]  = S(prev);
        }
        // Smooth crossfade for seamless loop
        int fade = Rate / 4;
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            b[i]         = (short)(b[i] * t);
            b[n - 1 - i] = (short)(b[n - 1 - i] * t);
        }
        return b;
    }

    private static short[] AmbientForest()
    {
        // Similar to outdoor but slightly brighter noise character
        int n = (int)(Rate * 3f);
        var b = new short[n];
        var r = new Random(200);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (r.NextSingle() * 2f - 1f) * 0.07f;
            prev = prev * 0.97f + white * 0.03f;
            b[i] = S(prev);
        }
        return b;
    }

    private static short[] AmbientDungeon()
    {
        // Low drone: two detuned sine waves
        int n = (int)(Rate * 4f);
        var b = new short[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            b[i] = S(MathF.Sin(MathF.Tau * 52f * t) * 0.18f
                   + MathF.Sin(MathF.Tau * 73.5f * t) * 0.11f);
        }
        // Seamless loop fade
        int fade = Rate / 3;
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            b[i]         = (short)(b[i] * t);
            b[n - 1 - i] = (short)(b[n - 1 - i] * t);
        }
        return b;
    }
}
