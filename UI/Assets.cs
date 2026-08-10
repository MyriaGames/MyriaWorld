using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.UI;

public static class Assets
{
    public static SpriteFont FontSmall  { get; private set; } = null!;
    public static SpriteFont FontNormal { get; private set; } = null!;
    public static SpriteFont FontMedium { get; private set; } = null!;
    public static Texture2D  Dot        { get; private set; } = null!;

    public static void Load(GraphicsDevice gd, ContentManager content)
    {
        Gfx.Init(gd);
        FontSmall  = content.Load<SpriteFont>("Fonts/FontSmall");
        FontNormal = content.Load<SpriteFont>("Fonts/FontNormal");
        FontMedium = content.Load<SpriteFont>("Fonts/FontMedium");
        Dot = CreateCircle(gd, 14);
    }

    // ── Text safety ───────────────────────────────────────────────────────────
    // Supported Unicode ranges (must match the CharacterRegions in *.spritefont):
    //   0x0020–0x007E  ASCII printable
    //   0x00A0–0x00FF  Latin-1 Supplement  (×, é, ä, ñ, …)
    //   0x2000–0x206F  General Punctuation (—, –, …, ", ", ', ')
    //   0x000A         newline (handled natively by SpriteBatch)

    public static string SafeString(string? text)
    {
        if (text is null) return "";

        // Fast path: scan for any unsupported char before allocating
        bool dirty = false;
        foreach (char c in text)
        {
            if (!IsSupported(c)) { dirty = true; break; }
        }
        if (!dirty) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(IsSupported(c) ? c : '?');
        return sb.ToString();
    }

    private static bool IsSupported(char c) =>
        c == '\n'                       ||   // newline
        (c >= 0x0020 && c <= 0x007E)   ||   // ASCII printable
        (c >= 0x00A0 && c <= 0x00FF)   ||   // Latin-1 Supplement
        (c >= 0x2000 && c <= 0x206F);        // General Punctuation

    private static Texture2D CreateCircle(GraphicsDevice gd, int radius)
    {
        int size = radius * 2;
        var data = new Color[size * size];
        var center = new Vector2(radius - 0.5f, radius - 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                data[y * size + x] = Vector2.Distance(new Vector2(x, y), center) <= radius - 0.5f
                    ? Color.White : Color.Transparent;
        var tex = new Texture2D(gd, size, size);
        tex.SetData(data);
        return tex;
    }
}
