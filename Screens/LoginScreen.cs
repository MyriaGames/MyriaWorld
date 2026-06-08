using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaWorld.UI;

namespace MyriaWorld.Screens;

/// <summary>Serves as both the Login and Register screens — same layout, different title/actions.</summary>
public class LoginScreen : Screen
{
    private readonly bool _isLogin;

    private Rectangle _panel;
    private UiTextBox _tbUsername = null!;
    private UiTextBox _tbPassword = null!;
    private UiButton  _btnAction  = null!;
    private UiButton  _btnCancel  = null!;

    private string _usernameMsg = "";
    private string _passwordMsg = "";
    private string _generalMsg  = "";

    private int _sw, _sh;

    // Decorative element positions
    private Vector2 _dotPos, _titlePos, _dividerPos;

    public LoginScreen(bool isLogin)
    {
        _isLogin = isLogin;
    }

    public override void LoadContent()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;

        const int panelW  = 320;
        const int padding = 24;

        int panelH  = EstimatePanelHeight();
        int panelX  = (_sw - panelW) / 2;
        int panelY  = (_sh - panelH) / 2;
        _panel = new Rectangle(panelX, panelY, panelW, panelH);

        int px = panelX + padding;
        int pw = panelW - padding * 2;

        // Title row
        int titleRowY = panelY + padding;
        int dotSize = 26;
        _dotPos   = new Vector2(px, titleRowY + 2);
        _titlePos = new Vector2(px + dotSize + 12, titleRowY + 2);

        // Divider below title
        int divY = titleRowY + Assets.FontMedium.LineSpacing + 14;
        _dividerPos = new Vector2(px, divY);

        int y = divY + 24;

        const int fieldH  = 42;
        const int labelGap = 6;
        const int msgH     = 18;
        const int fieldGap = 8;

        // Username field
        y += Assets.FontNormal.LineSpacing + labelGap;
        _tbUsername = new UiTextBox(new Rectangle(px, y, pw, fieldH), Assets.FontNormal);
        y += fieldH + fieldGap + msgH + 12;

        // Password field
        y += Assets.FontNormal.LineSpacing + labelGap;
        _tbPassword = new UiTextBox(new Rectangle(px, y, pw, fieldH), Assets.FontNormal) { IsPassword = true };
        y += fieldH + fieldGap + msgH + 18;

        // Action buttons
        const int btnH    = 42;
        const int btnGap  = 12;
        int halfW = (pw - btnGap) / 2;

        string actionLabel = _isLogin ? "Login" : "Register";
        _btnAction = new UiButton(
            new Rectangle(px, y, halfW, btnH),
            actionLabel, Assets.FontNormal, OnAction);
        _btnCancel = new UiButton(
            new Rectangle(px + halfW + btnGap, y, halfW, btnH),
            "Cancel", Assets.FontNormal, OnCancel);
    }

    private static int EstimatePanelHeight()
    {
        const int padding  = 24;
        const int titleH   = 30;
        const int dividerH = 24;
        const int sectionH = 18 + 6 + 42 + 8 + 18 + 12; // label + gap + field + gap + msg + margin
        const int buttonsH = 42;
        return padding + titleH + dividerH + sectionH * 2 + buttonsH + padding;
    }

    public override void Update(GameTime gt)
    {
        _tbUsername.Update(gt);
        _tbPassword.Update(gt);
        _btnAction.Update();
        _btnCancel.Update();

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape)) OnCancel();
    }

    public override void Draw(SpriteBatch sb)
    {
        ScreenManager.Instance.GraphicsDevice.Clear(Theme.Background);

        sb.Begin();

        // Subtle background tint
        Gfx.Rect(sb, 0, 0, _sw, _sh, new Color(10, 9, 14, 180));

        // Panel
        Gfx.Rect(sb, _panel, Theme.PanelBg);
        Gfx.Border(sb, _panel, Theme.Gold);

        // Decorative inner ellipses (top-right corner of panel, clipped by panel border)
        int ex = _panel.Right - 78;
        int ey = _panel.Y - 92;
        DrawEllipseArc(sb, ex, ey, 190, 190, Theme.Gold * 0.16f, _panel);
        DrawEllipseArc(sb, _panel.Right - 26, _panel.Y - 44, 126, 126, Theme.Gold * 0.12f, _panel);

        // Circle dot + title
        int dotSize = 26;
        sb.Draw(Assets.Dot, new Rectangle((int)_dotPos.X, (int)_dotPos.Y, dotSize, dotSize), Theme.GoldSoft);
        string title = _isLogin ? "Login" : "Register";
        Gfx.Text(sb, Assets.FontMedium, title, _titlePos, Theme.GoldSoft);

        // Divider
        int px = _panel.X + 24;
        int pr = _panel.Right - 24;
        int dy = (int)_dividerPos.Y;
        Gfx.Rect(sb, px, dy, pr - px, 1, Theme.Gold * 0.65f);
        Gfx.Diamond(sb, new Vector2((_panel.X + _panel.Right) / 2f, dy), 8, Theme.GoldSoft);

        // Username section
        int labelY = _tbUsername.Bounds.Y - Assets.FontNormal.LineSpacing - 6;
        Gfx.Text(sb, Assets.FontNormal, "Username", new Vector2(px, labelY), Theme.Foreground);
        _tbUsername.Draw(sb);
        if (_usernameMsg.Length > 0)
            Gfx.Text(sb, Assets.FontSmall, _usernameMsg, new Vector2(px, _tbUsername.Bounds.Bottom + 4), Theme.Error);

        // Password section
        labelY = _tbPassword.Bounds.Y - Assets.FontNormal.LineSpacing - 6;
        Gfx.Text(sb, Assets.FontNormal, "Password", new Vector2(px, labelY), Theme.Foreground);
        _tbPassword.Draw(sb);
        if (_passwordMsg.Length > 0)
            Gfx.Text(sb, Assets.FontSmall, _passwordMsg, new Vector2(px, _tbPassword.Bounds.Bottom + 4), Theme.Error);

        // General message (above buttons)
        if (_generalMsg.Length > 0)
            Gfx.Text(sb, Assets.FontSmall, _generalMsg, new Vector2(px, _btnAction.Bounds.Y - 20), Theme.Error);

        _btnAction.Draw(sb);
        _btnCancel.Draw(sb);

        sb.End();
    }

    public override void OnTextInput(char c)
    {
        _tbUsername.OnTextInput(c);
        _tbPassword.OnTextInput(c);
    }

    private void OnAction()
    {
        _usernameMsg = "";
        _passwordMsg = "";
        _generalMsg  = "";

        if (_tbUsername.Text.Trim().Length == 0) { _usernameMsg = "Username is required."; return; }
        if (_tbPassword.Text.Length == 0)        { _passwordMsg = "Password is required.";  return; }

        if (_isLogin)
            HandleLogin();
        else
            HandleRegister();
    }

    private void HandleLogin()
    {
        // TODO: call ServerApiService.LoginAsync then navigate to character selection
        _generalMsg = "Login not yet implemented.";
    }

    private void HandleRegister()
    {
        // TODO: call ServerApiService.RegisterAsync then navigate to login
        _generalMsg = "Registration not yet implemented.";
    }

    private void OnCancel() => ScreenManager.Instance.GoBack();

    private static void DrawEllipseArc(SpriteBatch sb, int cx, int cy, int rx, int ry, Color c, Rectangle clip)
    {
        const int segments = 48;
        Vector2 prev = new Vector2(cx + rx, cy);
        for (int i = 1; i <= segments; i++)
        {
            float angle = MathF.PI * 2f * i / segments;
            var next = new Vector2(cx + MathF.Cos(angle) * rx, cy + MathF.Sin(angle) * ry);
            if (clip.Contains((int)next.X, (int)next.Y) || clip.Contains((int)prev.X, (int)prev.Y))
                Gfx.Line(sb, prev, next, c);
            prev = next;
        }
    }
}
