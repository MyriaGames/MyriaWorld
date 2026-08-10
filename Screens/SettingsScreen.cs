using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myria.Mono.Services;
using Myria.Mono.UI;

namespace Myria.Mono.Screens;

/// <summary>SC3 — Display settings with persist + apply.</summary>
public class SettingsScreen : Screen
{
    // ── Layout ────────────────────────────────────────────────────────────────
    private int _sw, _sh;
    private const int PanW = 580, PanH = 440;
    private int _px, _py;

    // ── Working copy of settings (committed on Apply) ─────────────────────────
    private bool _fullscreen;
    private int  _resIdx;

    // ── Feedback flash ────────────────────────────────────────────────────────
    private float  _appliedTimer;
    private const float AppliedTime = 2f;

    // ── Input ─────────────────────────────────────────────────────────────────
    private MouseState    _prevMouse;
    private KeyboardState _prevKb;

    // ── Row helper geometry ────────────────────────────────────────────────────
    private const int RowH  = 52;
    private const int RowGap = 8;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void LoadContent()
    {
        Reposition();
        // Clone current settings into working copy
        _fullscreen = SettingsService.Current.Fullscreen;
        _resIdx     = SettingsService.Current.ResolutionIndex;
    }

    public override void OnEnter()
    {
        Reposition();
        Game1.Instance.IsMouseVisible = true;
        _prevMouse = Mouse.GetState();
    }

    private void Reposition()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;
        _px = (_sw - PanW) / 2;
        _py = (_sh - PanH) / 2;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(GameTime gt)
    {
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        var kb = Keyboard.GetState();
        var ms = Mouse.GetState();

        if (_appliedTimer > 0f) _appliedTimer -= dt;

        if (WasKeyJustPressed(kb, Keys.Escape)) { ScreenManager.Instance.GoBack(); _prevKb = kb; return; }

        bool clicked = ms.LeftButton == ButtonState.Released
                    && _prevMouse.LeftButton == ButtonState.Pressed;

        if (clicked)
        {
            var pos = ms.Position;

            // Fullscreen toggle
            if (FullscreenToggleRect().Contains(pos))
                _fullscreen = !_fullscreen;

            // Resolution arrows
            if (ResLeftRect().Contains(pos))
                _resIdx = Math.Max(0, _resIdx - 1);
            if (ResRightRect().Contains(pos))
                _resIdx = Math.Min(AppSettings.Resolutions.Length - 1, _resIdx + 1);

            // Apply button
            if (ApplyRect().Contains(pos))
                ApplySettings();

            // Back button
            if (BackRect().Contains(pos))
                ScreenManager.Instance.GoBack();
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
        const string title = "Settings";
        var tSz = Assets.FontMedium.MeasureString(title);
        Gfx.Text(sb, Assets.FontMedium, title,
            new Vector2(_px + (PanW - tSz.X) / 2f, _py + 12), Theme.GoldSoft);

        int cy = _py + 58;

        // ── Display section ───────────────────────────────────────────────────
        DrawSectionHeader(sb, "Display", cy);
        cy += 24;

        // Fullscreen row
        DrawRowLabel(sb, "Fullscreen", cy);
        var ftR = FullscreenToggleRect();
        Gfx.Rect(sb, ftR, _fullscreen ? Theme.BlueActive : Theme.PanelDark);
        Gfx.Border(sb, ftR, Theme.Gold * 0.7f);
        Gfx.TextCentered(sb, Assets.FontNormal, _fullscreen ? "ON" : "OFF", ftR,
            _fullscreen ? Theme.GoldSoft : Theme.ForegroundDim);
        cy += RowH + RowGap;

        // Resolution row
        DrawRowLabel(sb, "Resolution", cy);
        var (_, _, label) = AppSettings.Resolutions[_resIdx];
        DrawArrowButton(sb, ResLeftRect(),  "<", _resIdx > 0);
        DrawArrowButton(sb, ResRightRect(), ">", _resIdx < AppSettings.Resolutions.Length - 1);
        var lSz = Assets.FontNormal.MeasureString(label);
        Gfx.Text(sb, Assets.FontNormal, label,
            new Vector2(_px + 190 + (240 - lSz.X) / 2f, cy + (RowH - lSz.Y) / 2f),
            Theme.Foreground);
        cy += RowH + RowGap + 12;

        // ── Audio section (placeholder) ───────────────────────────────────────
        DrawSectionHeader(sb, "Audio", cy);
        cy += 24;
        Gfx.Text(sb, Assets.FontNormal, "Audio controls coming in a future update.",
            new Vector2(_px + 24, cy + 14), Theme.ForegroundDim * 0.5f);
        cy += RowH + RowGap;

        // ── Bottom buttons ────────────────────────────────────────────────────
        Gfx.Rect(sb, _px + 20, _py + PanH - 80, PanW - 40, 1, Theme.Gold * 0.3f);
        DrawActionButton(sb, ApplyRect(), "Apply & Save", active: true);
        DrawActionButton(sb, BackRect(),  "Back",         active: true);

        // "Applied!" flash
        if (_appliedTimer > 0f)
        {
            float alpha = MathHelper.Clamp(_appliedTimer, 0f, 1f);
            const string msg = "Settings applied!";
            var mSz = Assets.FontNormal.MeasureString(msg);
            Gfx.Text(sb, Assets.FontNormal, msg,
                new Vector2(_px + (PanW - mSz.X) / 2f, _py + PanH - 90),
                new Color(100, 200, 100) * alpha);
        }

        // ESC hint
        Gfx.Text(sb, Assets.FontSmall, "ESC — Back without saving",
            new Vector2(_px + 14, _py + PanH - 20), Theme.ForegroundDim);

        sb.End();
    }

    // ── Draw helpers ──────────────────────────────────────────────────────────

    private void DrawSectionHeader(SpriteBatch sb, string text, int y)
    {
        Gfx.Text(sb, Assets.FontSmall, text, new Vector2(_px + 24, y), Theme.ForegroundDim);
        Gfx.Rect(sb, _px + 24, y + 16, PanW - 48, 1, Theme.Gold * 0.25f);
    }

    private void DrawRowLabel(SpriteBatch sb, string text, int y)
    {
        var sz = Assets.FontNormal.MeasureString(text);
        Gfx.Text(sb, Assets.FontNormal, text,
            new Vector2(_px + 24, y + (RowH - sz.Y) / 2f), Theme.Foreground);
    }

    private static void DrawArrowButton(SpriteBatch sb, Rectangle r, string arrow, bool enabled)
    {
        float a = enabled ? 1f : 0.25f;
        Gfx.Rect(sb, r, Theme.PanelDark * a);
        Gfx.Border(sb, r, Theme.Gold * a * 0.7f);
        Gfx.TextCentered(sb, Assets.FontNormal, arrow, r, Theme.GoldSoft * a);
    }

    private static void DrawActionButton(SpriteBatch sb, Rectangle r, string label, bool active)
    {
        float a = active ? 1f : 0.3f;
        Gfx.Rect(sb, r, Theme.PanelDark * a);
        Gfx.Border(sb, r, Theme.Gold * a);
        Gfx.TextCentered(sb, Assets.FontNormal, label, r, Theme.GoldSoft * a);
    }

    // ── Rectangle helpers ─────────────────────────────────────────────────────

    private int Row0Y => _py + 82;
    private int Row1Y => Row0Y + RowH + RowGap;

    private Rectangle FullscreenToggleRect() =>
        new(_px + PanW - 110, Row0Y + (RowH - 36) / 2, 90, 36);

    private Rectangle ResLeftRect()  => new(_px + 186, Row1Y + (RowH - 36) / 2, 36, 36);
    private Rectangle ResRightRect() => new(_px + 430, Row1Y + (RowH - 36) / 2, 36, 36);

    private Rectangle ApplyRect() => new(_px + 24,          _py + PanH - 66, 240, 48);
    private Rectangle BackRect()  => new(_px + PanW - 264,  _py + PanH - 66, 240, 48);

    // ── Actions ───────────────────────────────────────────────────────────────

    private void ApplySettings()
    {
        SettingsService.Current.Fullscreen      = _fullscreen;
        SettingsService.Current.ResolutionIndex = _resIdx;
        SettingsService.Save();
        SettingsService.Apply();

        // Panel position changes after resolution flip — recompute
        Reposition();
        _appliedTimer = AppliedTime;
    }

    private bool WasKeyJustPressed(KeyboardState cur, Keys key)
        => cur.IsKeyDown(key) && !_prevKb.IsKeyDown(key);
}
