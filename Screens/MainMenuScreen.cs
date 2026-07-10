using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaWorld.UI;

namespace MyriaWorld.Screens;

public class MainMenuScreen : Screen
{
    private List<UiButton> _buttons = new();

    // Layout geometry, computed once in LoadContent
    private int _sw, _sh;
    private Rectangle _panel;

    // Decorative element positions
    private Vector2 _dotPos;
    private Vector2 _titlePos;
    private int _dividerY;
    private int _dividerThin1Y;
    private int _dividerThin2Y;

    public override void LoadContent()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;

        const int panelW  = 360;
        const int margin  = 34;
        const int padding = 24;

        _panel = new Rectangle(_sw - panelW - margin, margin, panelW, _sh - margin * 2);

        int px = _panel.X + padding;
        int pw = _panel.Width - padding * 2;  // inner width

        // Title row: circle dot (28×28) + "Myria" in FontMedium
        int titleRowY = _panel.Y + padding;
        int dotSize = 28;
        _dotPos   = new Vector2(px, titleRowY + 1);
        _titlePos = new Vector2(px + dotSize + 14, titleRowY + 2);

        // First ornamental divider (with diamond)
        int titleBottom = titleRowY + Assets.FontMedium.LineSpacing + 18;
        _dividerY = titleBottom;

        // Buttons start below the divider
        int btnY = _dividerY + 24;
        int btnH = 54;

        _buttons.Add(new UiButton(new Rectangle(px, btnY, pw, btnH), "Single Character", Assets.FontNormal, OnSingleCharacter));
        btnY += btnH + 10;

        // Thin divider #1
        _dividerThin1Y = btnY + 6;
        btnY += 6 + 1 + 16; // top margin + line + bottom margin

        _buttons.Add(new UiButton(new Rectangle(px, btnY, pw, btnH), "Login",    Assets.FontNormal, OnLogin));
        btnY += btnH + 10;
        _buttons.Add(new UiButton(new Rectangle(px, btnY, pw, btnH), "Register", Assets.FontNormal, OnRegister));
        btnY += btnH + 10;

        // Thin divider #2
        _dividerThin2Y = btnY + 6;
        btnY += 6 + 1 + 16;

        _buttons.Add(new UiButton(new Rectangle(px, btnY, pw, btnH), "Settings", Assets.FontNormal, OnSettings));
        btnY += btnH + 10;
        _buttons.Add(new UiButton(new Rectangle(px, btnY, pw, btnH), "Quit",     Assets.FontNormal, OnQuit));
    }

    public override void Update(GameTime gt)
    {
        foreach (var btn in _buttons) btn.Update();
    }

    public override void Draw(SpriteBatch sb)
    {
        var gd = ScreenManager.Instance.GraphicsDevice;
        gd.Clear(Theme.Background);

        sb.Begin();

        // Subtle radial gradient on left side using a large translucent ellipse-ish overlay
        Gfx.Rect(sb, 0, 0, _sw - _panel.Width - 34, _sh, new Color(15, 12, 20, 80));

        // Background decorative ellipses (top-right, partially behind panel)
        DrawEllipseOutline(sb, _sw - 90, -150, 520, 520, Theme.Gold * 0.22f);
        DrawEllipseOutline(sb, _sw - 8, -72, 340, 340, Theme.Gold * 0.17f);

        // Panel
        Gfx.Rect(sb, _panel, Theme.PanelBg);
        Gfx.Border(sb, _panel, Theme.Gold);

        // Circle dot
        int dotSize = 28;
        sb.Draw(Assets.Dot, new Rectangle((int)_dotPos.X, (int)_dotPos.Y, dotSize, dotSize), Theme.GoldSoft);

        // "Myria" title
        Gfx.Text(sb, Assets.FontMedium, "Myria", _titlePos, Theme.GoldSoft);

        // Ornamental divider
        int px = _panel.X + 24;
        int pr = _panel.Right - 24;
        Gfx.Rect(sb, px, _dividerY, pr - px, 1, Theme.Gold * 0.65f);
        Gfx.Diamond(sb, new Vector2((_panel.X + _panel.Right) / 2f, _dividerY), 8, Theme.GoldSoft);

        // Thin separator #1
        Gfx.Rect(sb, px, _dividerThin1Y, pr - px, 1, Theme.Gold * 0.35f);

        // Thin separator #2
        Gfx.Rect(sb, px, _dividerThin2Y, pr - px, 1, Theme.Gold * 0.35f);

        // Buttons
        foreach (var btn in _buttons) btn.Draw(sb);

        sb.End();
    }

    // Approximate ellipse outline with line segments
    private static void DrawEllipseOutline(SpriteBatch sb, int cx, int cy, int rx, int ry, Color c)
    {
        const int segments = 48;
        Vector2 prev = new Vector2(cx + rx, cy);
        for (int i = 1; i <= segments; i++)
        {
            float angle = MathF.PI * 2f * i / segments;
            var next = new Vector2(cx + MathF.Cos(angle) * rx, cy + MathF.Sin(angle) * ry);
            Gfx.Line(sb, prev, next, c);
            prev = next;
        }
    }

    private void OnSingleCharacter()
    {
        /*
        if (GameDebug.Active)
        {
            // Debug shortcut: skip character selection and jump straight in
            var player = GameDebug.CreateDummyCharacter();
            ScreenManager.Instance.Navigate(new LoadingScreen(player));
            return;
        }*/
        ScreenManager.Instance.Navigate(new CharacterSelectScreen());
    }
    private void OnLogin()     => ScreenManager.Instance.Navigate(new LoginScreen(isLogin: true));
    private void OnRegister()  => ScreenManager.Instance.Navigate(new LoginScreen(isLogin: false));
    private void OnSettings()  => ScreenManager.Instance.Navigate(new SettingsScreen());
    private void OnQuit()      => Environment.Exit(0);
}
