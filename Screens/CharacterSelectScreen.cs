using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myria.Mono.Services;
using Myria.Mono.UI;

namespace Myria.Mono.Screens;

/// <summary>SC2 — Local character list; pick a character or create a new one.</summary>
public class CharacterSelectScreen : Screen
{
    // ── Layout ────────────────────────────────────────────────────────────────
    private int _sw, _sh;
    private const int PanW = 800, PanH = 580;
    private int _px, _py;

    private const int CardH    = 68;
    private const int CardGap  = 6;
    private const int ListX    = 30;
    private const int ListW    = PanW - 60;
    private const int ListTopY = 92;      // first card y (relative to panel top)
    private const int MaxVisible = 6;     // max cards shown at once

    // ── State ─────────────────────────────────────────────────────────────────
    private List<LocalSaveService.CharacterSummary> _characters = new();
    private int  _selected    = -1;
    private int  _scrollOffset = 0;       // top card index (for scroll)
    private bool _confirmDelete;

    // ── Input ─────────────────────────────────────────────────────────────────
    private MouseState    _prevMouse;
    private KeyboardState _prevKb;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void LoadContent()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;
        _px = (_sw - PanW) / 2;
        _py = (_sh - PanH) / 2;
    }

    public override void OnEnter()
    {
        Game1.Instance.IsMouseVisible = true;
        Refresh();
        _prevMouse = Mouse.GetState();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(GameTime gt)
    {
        var kb = Keyboard.GetState();
        var ms = Mouse.GetState();

        if (WasKeyJustPressed(kb, Keys.Escape))
        {
            if (_confirmDelete) { _confirmDelete = false; }
            else ScreenManager.Instance.GoBack();
            _prevKb = kb; return;
        }

        // Keyboard navigation of the list
        if (WasKeyJustPressed(kb, Keys.Up)   && _selected > 0)                  { _selected--; ClampScroll(); }
        if (WasKeyJustPressed(kb, Keys.Down) && _selected < _characters.Count - 1) { _selected++; ClampScroll(); }
        if (WasKeyJustPressed(kb, Keys.Enter) && _selected >= 0 && !_confirmDelete)
            PlaySelected();

        bool clicked = ms.LeftButton  == ButtonState.Released
                    && _prevMouse.LeftButton == ButtonState.Pressed;

        if (clicked)
        {
            var pos = ms.Position;

            // Character card list
            for (int vi = 0; vi < MaxVisible; vi++)
            {
                int ci = vi + _scrollOffset;
                if (ci >= _characters.Count) break;
                var card = CardRect(vi);
                if (card.Contains(pos))
                {
                    if (_selected == ci && !_confirmDelete) PlaySelected();
                    else { _selected = ci; _confirmDelete = false; }
                }
            }

            // Buttons
            if (PlayButtonRect().Contains(pos)        && _selected >= 0) PlaySelected();
            if (NewButtonRect().Contains(pos))        GoCreate();
            if (DeleteButtonRect().Contains(pos)      && _selected >= 0) PromptDelete();
            if (_confirmDelete && ConfirmYesRect().Contains(pos)) DoDelete();
            if (_confirmDelete && ConfirmNoRect().Contains(pos))  _confirmDelete = false;

            // Scroll arrows
            if (UpArrowRect().Contains(pos)   && _scrollOffset > 0)
                _scrollOffset--;
            if (DownArrowRect().Contains(pos) && _scrollOffset + MaxVisible < _characters.Count)
                _scrollOffset++;
        }

        _prevMouse = ms;
        _prevKb    = kb;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public override void Draw(SpriteBatch sb)
    {
        ScreenManager.Instance.GraphicsDevice.Clear(Theme.Background);
        sb.Begin();

        // Panel
        Gfx.Rect(sb, _px, _py, PanW, PanH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(_px, _py, PanW, PanH), Theme.Gold);

        // Title
        const string title = "Select Character";
        var tSz = Assets.FontMedium.MeasureString(title);
        Gfx.Text(sb, Assets.FontMedium, title,
            new Vector2(_px + (PanW - tSz.X) / 2f, _py + 10), Theme.GoldSoft);

        // Divider below title
        Gfx.Rect(sb, _px + 20, _py + 50, PanW - 40, 1, Theme.Gold * 0.4f);

        // Character list
        if (_characters.Count == 0)
        {
            const string empty = "No saved characters.  Create one below.";
            var eSz = Assets.FontNormal.MeasureString(empty);
            Gfx.Text(sb, Assets.FontNormal, empty,
                new Vector2(_px + (PanW - eSz.X) / 2f, _py + ListTopY + 60),
                Theme.ForegroundDim);
        }
        else
        {
            for (int vi = 0; vi < MaxVisible; vi++)
            {
                int ci = vi + _scrollOffset;
                if (ci >= _characters.Count) break;
                DrawCard(sb, vi, ci, _characters[ci]);
            }

            // Scroll arrows
            if (_scrollOffset > 0)
                DrawArrow(sb, UpArrowRect(), up: true);
            if (_scrollOffset + MaxVisible < _characters.Count)
                DrawArrow(sb, DownArrowRect(), up: false);
        }

        // Bottom button bar
        Gfx.Rect(sb, _px + 20, _py + PanH - 78, PanW - 40, 1, Theme.Gold * 0.3f);
        DrawActionButton(sb, PlayButtonRect(),   "Play",        _selected >= 0);
        DrawActionButton(sb, NewButtonRect(),    "New Character", true);
        DrawActionButton(sb, DeleteButtonRect(), "Delete",      _selected >= 0);

        // Confirm-delete overlay
        if (_confirmDelete) DrawConfirmDelete(sb);

        // ESC hint
        Gfx.Text(sb, Assets.FontSmall, "ESC — Back   Up/Down — Navigate   Enter — Play",
            new Vector2(_px + 14, _py + PanH - 20), Theme.ForegroundDim);

        sb.End();
    }

    private void DrawCard(SpriteBatch sb, int visualIdx, int charIdx,
                          LocalSaveService.CharacterSummary ch)
    {
        bool sel = charIdx == _selected;
        var  r   = CardRect(visualIdx);

        Gfx.Rect(sb, r, sel ? Theme.BlueActive : Theme.PanelDark);
        Gfx.Border(sb, r, sel ? Theme.Gold : Theme.Gold * 0.25f);
        if (sel) Gfx.Rect(sb, r.X, r.Y, 4, r.Height, Theme.Gold);

        // Name + level
        Gfx.Text(sb, Assets.FontNormal, ch.Name,
            new Vector2(r.X + 14, r.Y + 10),
            sel ? Theme.GoldSoft : Theme.Foreground);

        string sub = $"Lv. {ch.Level}  {ch.Race}  {ch.Class}";
        Gfx.Text(sb, Assets.FontSmall, sub,
            new Vector2(r.X + 14, r.Y + 36), Theme.ForegroundDim);

        // Level badge (right side)
        string lvl = $"Lv {ch.Level}";
        var lvSz = Assets.FontNormal.MeasureString(lvl);
        Gfx.Text(sb, Assets.FontNormal, lvl,
            new Vector2(r.Right - lvSz.X - 14, r.Y + 10 + (CardH - 30) / 2f),
            sel ? Theme.Gold : Theme.ForegroundDim);
    }

    private void DrawActionButton(SpriteBatch sb, Rectangle r, string label, bool enabled)
    {
        float a = enabled ? 1f : 0.3f;
        Gfx.Rect(sb, r, Theme.PanelDark * a);
        Gfx.Border(sb, r, Theme.Gold * a);
        Gfx.TextCentered(sb, Assets.FontNormal, label, r, Theme.GoldSoft * a);
    }

    private void DrawArrow(SpriteBatch sb, Rectangle r, bool up)
    {
        Gfx.Rect(sb, r, Theme.PanelDark);
        Gfx.Border(sb, r, Theme.Gold * 0.4f);
        string arrow = up ? "^" : "v";
        Gfx.TextCentered(sb, Assets.FontNormal, arrow, r, Theme.ForegroundDim);
    }

    private void DrawConfirmDelete(SpriteBatch sb)
    {
        // Dim overlay
        Gfx.Rect(sb, _px, _py, PanW, PanH, new Color(0, 0, 0, 160));

        int dw = 380, dh = 130;
        int dx = _px + (PanW - dw) / 2;
        int dy = _py + (PanH - dh) / 2;

        Gfx.Rect(sb, dx, dy, dw, dh, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(dx, dy, dw, dh), new Color(200, 80, 80));

        string name  = _characters[_selected].Name;
        string msg   = $"Delete '{name}'?  This cannot be undone.";
        var mSz = Assets.FontSmall.MeasureString(msg);
        Gfx.Text(sb, Assets.FontSmall, msg,
            new Vector2(dx + (dw - mSz.X) / 2f, dy + 20), Theme.Foreground);

        DrawActionButton(sb, ConfirmYesRect(), "Delete", true);
        DrawActionButton(sb, ConfirmNoRect(),  "Cancel", true);
    }

    // ── Rectangle helpers ─────────────────────────────────────────────────────

    private Rectangle CardRect(int visualIdx) =>
        new(_px + ListX, _py + ListTopY + visualIdx * (CardH + CardGap), ListW, CardH);

    private Rectangle UpArrowRect() =>
        new(_px + PanW - 44, _py + ListTopY, 30, 24);

    private Rectangle DownArrowRect() =>
        new(_px + PanW - 44, _py + ListTopY + MaxVisible * (CardH + CardGap) - 24, 30, 24);

    private int _btnY  => _py + PanH - 68;
    private int _btnH  => 52;

    private Rectangle PlayButtonRect()   => new(_px + 30,         _btnY, 210, _btnH);
    private Rectangle NewButtonRect()    => new(_px + 30 + 220,   _btnY, 290, _btnH);
    private Rectangle DeleteButtonRect() => new(_px + 30 + 520,   _btnY, 210, _btnH);

    private Rectangle ConfirmYesRect() =>
        new(_px + (PanW - 380) / 2 + 10, _py + (PanH - 130) / 2 + 60, 160, 44);
    private Rectangle ConfirmNoRect()  =>
        new(_px + (PanW - 380) / 2 + 210, _py + (PanH - 130) / 2 + 60, 160, 44);

    // ── Actions ───────────────────────────────────────────────────────────────

    private void Refresh()
    {
        _characters    = LocalSaveService.List();
        _selected      = _characters.Count > 0 ? 0 : -1;
        _scrollOffset  = 0;
        _confirmDelete = false;
    }

    private void PlaySelected()
    {
        if (_selected < 0 || _selected >= _characters.Count) return;
        string name = _characters[_selected].Name;
        ScreenManager.Instance.Navigate(new LoadingScreen(name));
    }

    private void GoCreate()
    {
        ScreenManager.Instance.Navigate(new CharacterCreationScreen());
    }

    private void PromptDelete()
    {
        if (_selected < 0) return;
        _confirmDelete = true;
    }

    private void DoDelete()
    {
        if (_selected < 0 || _selected >= _characters.Count) return;
        LocalSaveService.Delete(_characters[_selected].Name);
        Refresh();
        _confirmDelete = false;
    }

    private void ClampScroll()
    {
        if (_selected < _scrollOffset) _scrollOffset = _selected;
        if (_selected >= _scrollOffset + MaxVisible) _scrollOffset = _selected - MaxVisible + 1;
        _scrollOffset = Math.Max(0, _scrollOffset);
    }

    private bool WasKeyJustPressed(KeyboardState cur, Keys key)
        => cur.IsKeyDown(key) && !_prevKb.IsKeyDown(key);
}
