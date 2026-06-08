using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MyriaWorld.UI;

public class UiButton
{
    private Rectangle _bounds;
    private string _text;
    private readonly SpriteFont _font;
    private readonly Action _onClick;
    private bool _hovered;
    private bool _pressed;
    private MouseState _prev;

    public bool      Enabled { get; set; } = true;
    public string    Text    { get => _text; set => _text = value; }
    public Rectangle Bounds  => _bounds;

    public UiButton(Rectangle bounds, string text, SpriteFont font, Action onClick)
    {
        _bounds  = bounds;
        _text    = text;
        _font    = font;
        _onClick = onClick;
    }

    public void Update()
    {
        if (!Enabled) return;
        var m = Mouse.GetState();
        _hovered = _bounds.Contains(m.Position);
        _pressed = _hovered && m.LeftButton == ButtonState.Pressed;
        if (_hovered && m.LeftButton == ButtonState.Released && _prev.LeftButton == ButtonState.Pressed)
            _onClick();
        _prev = m;
    }

    public void Draw(SpriteBatch sb)
    {
        float a = Enabled ? 1f : 0.35f;
        Color bg     = (_pressed ? Theme.BlueActive : _hovered ? Theme.NavHover : Theme.PanelDark) * a;
        Color border = Theme.Gold * a;
        Color text   = Theme.GoldSoft * a;

        Gfx.Rect(sb, _bounds, bg);
        Gfx.Border(sb, _bounds, border);

        if (_hovered && Enabled)
        {
            Gfx.Rect(sb, _bounds.X, _bounds.Y, 4, _bounds.Height, Theme.Gold * a);
            Gfx.Rect(sb, _bounds.Right - 4, _bounds.Y, 4, _bounds.Height, Theme.Gold * a);
        }

        Gfx.TextCentered(sb, _font, _text, _bounds, text);
    }
}
