using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MyriaWorld.UI;

public class UiTextBox
{
    private readonly Rectangle _bounds;
    private readonly SpriteFont _font;
    private string _text = "";
    private bool _focused;
    private double _blinkTimer;
    private bool _cursorOn = true;
    private MouseState _prev;

    public string    Text     => _text;
    public bool      IsPassword { get; set; }
    public Rectangle Bounds     => _bounds;

    public UiTextBox(Rectangle bounds, SpriteFont font)
    {
        _bounds = bounds;
        _font   = font;
    }

    public void Update(GameTime gt)
    {
        var m = Mouse.GetState();
        if (m.LeftButton == ButtonState.Pressed && _prev.LeftButton == ButtonState.Released)
            _focused = _bounds.Contains(m.Position);
        _prev = m;

        if (_focused)
        {
            _blinkTimer += gt.ElapsedGameTime.TotalSeconds;
            if (_blinkTimer >= 0.53)
            {
                _blinkTimer = 0;
                _cursorOn = !_cursorOn;
            }
        }
    }

    public void OnTextInput(char c)
    {
        if (!_focused) return;
        if (c == '\b')
        {
            if (_text.Length > 0) _text = _text[..^1];
        }
        else if (!char.IsControl(c))
        {
            _text += c;
        }
    }

    public void Draw(SpriteBatch sb)
    {
        Gfx.Rect(sb, _bounds, _focused ? Theme.NavHover : Theme.PanelDark);
        Gfx.Border(sb, _bounds, Theme.Gold);

        string display = IsPassword ? new string('*', _text.Length) : _text;
        if (_focused && _cursorOn) display += "|";

        float textY = _bounds.Y + (_bounds.Height - _font.MeasureString("A").Y) / 2f;
        Gfx.Text(sb, _font, display, new Vector2(_bounds.X + 12, MathF.Floor(textY)), Theme.Foreground);
    }
}
