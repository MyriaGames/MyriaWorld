using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Myria.Mono.Services;

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine("Data", "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented          = true,
        PropertyNameCaseInsensitive = true,
    };

    public static AppSettings Current { get; private set; } = new();

    // ── Load / Save ───────────────────────────────────────────────────────────

    public static void Load()
    {
        if (!File.Exists(SettingsPath)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(SettingsPath), _opts);
            if (loaded != null) Current = loaded;
        }
        catch { /* malformed file — keep defaults */ }
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, _opts));
    }

    // ── Apply to the display ──────────────────────────────────────────────────

    public static void Apply()
    {
        var gdm = Game1.Display;
        var (w, h, _) = Current.CurrentResolution;
        gdm.PreferredBackBufferWidth  = w;
        gdm.PreferredBackBufferHeight = h;
        gdm.IsFullScreen              = Current.Fullscreen;
        gdm.ApplyChanges();
    }
}
