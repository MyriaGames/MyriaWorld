using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MyriaWorld.UI;

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
