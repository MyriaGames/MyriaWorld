using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyriaWorld.UI;

public static class Gfx
{
    private static Texture2D _pixel = null!;

    public static void Init(GraphicsDevice gd)
    {
        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public static void Rect(SpriteBatch sb, Rectangle r, Color c)
        => sb.Draw(_pixel, r, c);

    public static void Rect(SpriteBatch sb, int x, int y, int w, int h, Color c)
        => sb.Draw(_pixel, new Rectangle(x, y, w, h), c);

    public static void Border(SpriteBatch sb, Rectangle r, Color c, int t = 1)
    {
        Rect(sb, r.X, r.Y, r.Width, t, c);
        Rect(sb, r.X, r.Bottom - t, r.Width, t, c);
        Rect(sb, r.X, r.Y, t, r.Height, c);
        Rect(sb, r.Right - t, r.Y, t, r.Height, c);
    }

    public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color c, float thickness = 1f)
    {
        var diff = b - a;
        float angle = MathF.Atan2(diff.Y, diff.X);
        float length = diff.Length();
        sb.Draw(_pixel, a, null, c, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public static void Diamond(SpriteBatch sb, Vector2 center, int half, Color c, float thickness = 1f)
    {
        Line(sb, new Vector2(center.X, center.Y - half), new Vector2(center.X + half, center.Y), c, thickness);
        Line(sb, new Vector2(center.X + half, center.Y), new Vector2(center.X, center.Y + half), c, thickness);
        Line(sb, new Vector2(center.X, center.Y + half), new Vector2(center.X - half, center.Y), c, thickness);
        Line(sb, new Vector2(center.X - half, center.Y), new Vector2(center.X, center.Y - half), c, thickness);
    }

    public static void Text(SpriteBatch sb, SpriteFont font, string text, Vector2 pos, Color c)
    {
        var safe = Assets.SafeString(text);
        if (safe.Length > 0) sb.DrawString(font, safe, pos, c);
    }

    public static void TextCentered(SpriteBatch sb, SpriteFont font, string text, Rectangle r, Color c)
    {
        var safe = Assets.SafeString(text);
        if (safe.Length == 0) return;
        var size = font.MeasureString(safe);
        var pos  = new Vector2(r.X + (r.Width - size.X) / 2f, r.Y + (r.Height - size.Y) / 2f);
        sb.DrawString(font, safe, Vector2.Floor(pos), c);
    }
}
