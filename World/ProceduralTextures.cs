using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

/// <summary>
/// Generates small tileable Texture2Ds at load time — no art assets required.
/// Every 3D mesh in the game (props, characters, floor) was previously flat
/// vertex-colored boxes; these give surfaces real per-pixel grain/mottling
/// while the existing vertex colors keep doing per-face shading/tinting on top
/// (BasicEffect multiplies texture x vertex color when both are enabled).
/// </summary>
public static class ProceduralTextures
{
    /// <summary>Neutral (near-white) grain texture multiplied onto every prop/character
    /// mesh's existing vertex-color shading — turns flat plastic-looking boxes into
    /// something with visible surface detail without needing a texture per material.</summary>
    public static Texture2D Detail { get; private set; } = null!;

    /// <summary>Worn dirt path texture used by the trail-strip network connecting rooms.</summary>
    public static Texture2D Path { get; private set; } = null!;

    /// <summary>Trampled/scorched clearing decal used to mark monster spawn zones.</summary>
    public static Texture2D Clearing { get; private set; } = null!;

    private static readonly Dictionary<string, Texture2D> _terrain = new(StringComparer.OrdinalIgnoreCase);

    public static void Load(GraphicsDevice gd)
    {
        Detail   = MakeTile(gd, 64, new Color(238, 238, 238), new Color(198, 198, 198), 0.55f, seed: 1);
        Path     = MakeTile(gd, 64, new Color(168, 138, 96),  new Color(136, 108, 72),  0.45f, seed: 2);
        Clearing = MakeTile(gd, 64, new Color(96,  78,  56),  new Color(58,  46,  32),  0.60f, seed: 3);

        _terrain["grass"]    = MakeTile(gd, 64, new Color(64,  104, 48),  new Color(38, 70, 30),  0.55f, seed: 11);
        _terrain["forest"]   = MakeTile(gd, 64, new Color(42,  72,  36),  new Color(24, 48, 22),  0.55f, seed: 12);
        _terrain["dirt"]     = MakeTile(gd, 64, new Color(118, 86,  52),  new Color(82, 58, 32),  0.60f, seed: 13);
        _terrain["stone"]    = MakeTile(gd, 64, new Color(102, 100, 96),  new Color(64, 62, 60),  0.50f, seed: 14, grid: 16);
        _terrain["city"]     = MakeTile(gd, 64, new Color(124, 116, 104), new Color(92, 86, 78),  0.30f, seed: 15, grid: 16);
        _terrain["cave"]     = MakeTile(gd, 64, new Color(58,  54,  60),  new Color(30, 28, 34),  0.65f, seed: 16);
        _terrain["dungeon"]  = MakeTile(gd, 64, new Color(42,  36,  46),  new Color(20, 16, 24),  0.60f, seed: 17);
        _terrain["sand"]     = MakeTile(gd, 64, new Color(206, 180, 126), new Color(174, 148, 98), 0.40f, seed: 18);
        _terrain["snow"]     = MakeTile(gd, 64, new Color(232, 238, 244), new Color(204, 212, 222), 0.30f, seed: 19);
        _terrain["water"]    = MakeTile(gd, 64, new Color(46,  92,  158), new Color(24, 58, 112),  0.35f, seed: 20);
        _terrain["wood"]     = MakeTile(gd, 64, new Color(128, 94,  56),  new Color(94, 66, 36),   0.45f, seed: 21);
        _terrain["interior"] = MakeTile(gd, 64, new Color(158, 138, 108), new Color(126, 108, 84), 0.30f, seed: 22, grid: 16);
    }

    /// <summary>Returns the tileable floor texture for a terrain type string, or the
    /// "grass" tile if the terrain isn't one of the built-in tones.</summary>
    public static Texture2D TerrainTile(string terrain) =>
        _terrain.TryGetValue(terrain, out var t) ? t : _terrain["grass"];

    // ── Generation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a tileable size x size texture: per-pixel hash noise, box-blurred (wrapping)
    /// for a soft mottled look, then lerped between baseC/darkC. `grid`, if > 0, darkens a
    /// thin mortar/seam line every `grid` pixels (used for stone/city/interior floor tiles).
    /// </summary>
    private static Texture2D MakeTile(GraphicsDevice gd, int size, Color baseC, Color darkC,
        float roughness, int seed, int grid = 0)
    {
        var raw = new float[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                raw[x, y] = Hash(x + seed * 7919, y + seed * 104729);

        var data = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 3x3 box blur with wraparound indices — keeps the tile seamless.
                float sum = 0f;
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        sum += raw[(x + ox + size) % size, (y + oy + size) % size];
                float n = sum / 9f;

                Color c = Color.Lerp(baseC, darkC, MathHelper.Clamp(n * roughness * 2f, 0f, 1f));

                if (grid > 0 && (x % grid == 0 || y % grid == 0))
                    c = Color.Lerp(c, Color.Black, 0.28f);

                data[y * size + x] = c;
            }
        }

        var tex = new Texture2D(gd, size, size);
        tex.SetData(data);
        return tex;
    }

    private static float Hash(int x, int y)
    {
        int n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        n = n ^ (n >> 16);
        return (n & 0x7fffffff) / (float)int.MaxValue;
    }
}
