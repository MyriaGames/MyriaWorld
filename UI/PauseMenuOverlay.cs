using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MyriaWorld.UI;

/// <summary>
/// In-game pause menu shown when pressing ESC from the world.
/// Draws over the current frame so the world remains visible behind the dim overlay.
/// </summary>
public sealed class PauseMenuOverlay
{
    public bool IsOpen { get; private set; }

    private readonly List<UiButton> _buttons = new();

    // Callbacks supplied by WorldScreen so the overlay stays decoupled.
    private Action? _onResume;
    private Action? _onSave;
    private Action? _onLoad;
    private Action? _onQuit;

    private int   _panelX, _panelY;
    private const int PanelW  = 320;
    private const int BtnH    = 52;
    private const int BtnGap  = 10;
    private const int PadX    = 28;
    private const int TitleH  = 58;
    private const int FooterH = 40;

    // Total panel height: title + N buttons + footer
    private const int NumButtons = 4;
    private static int PanelH =>
        TitleH + NumButtons * BtnH + (NumButtons - 1) * BtnGap + FooterH + 12;

    /// <summary>
    /// Call once after the graphics device is ready to wire up callbacks and build the button list.
    /// </summary>
    public void Init(Action onResume, Action onSave, Action onLoad, Action onQuit)
    {
        _onResume = onResume;
        _onSave   = onSave;
        _onLoad   = onLoad;
        _onQuit   = onQuit;
    }

    public void Open(int screenW, int screenH)
    {
        IsOpen = true;

        _panelX = (screenW - PanelW) / 2;
        _panelY = (screenH - PanelH) / 2;

        int btnX = _panelX + PadX;
        int btnW = PanelW - PadX * 2;
        int btnY = _panelY + TitleH;

        _buttons.Clear();

        _buttons.Add(new UiButton(new Rectangle(btnX, btnY, btnW, BtnH),
            "Resume", Assets.FontNormal, () => { _onResume?.Invoke(); }));
        btnY += BtnH + BtnGap;

        _buttons.Add(new UiButton(new Rectangle(btnX, btnY, btnW, BtnH),
            "Save Game", Assets.FontNormal, () => { _onSave?.Invoke(); }));
        btnY += BtnH + BtnGap;

        _buttons.Add(new UiButton(new Rectangle(btnX, btnY, btnW, BtnH),
            "Load Game", Assets.FontNormal, () => { _onLoad?.Invoke(); }));
        btnY += BtnH + BtnGap;

        _buttons.Add(new UiButton(new Rectangle(btnX, btnY, btnW, BtnH),
            "Quit to Main Menu", Assets.FontNormal, () => { _onQuit?.Invoke(); }));
    }

    public void Close() => IsOpen = false;

    public void Update()
    {
        if (!IsOpen) return;
        foreach (var btn in _buttons) btn.Update();
    }

    public void Draw(SpriteBatch sb, int sw, int sh)
    {
        if (!IsOpen) return;

        // Full-screen dim
        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, 160));

        // Panel background + border
        var panel = new Rectangle(_panelX, _panelY, PanelW, PanelH);
        Gfx.Rect(sb, panel, Theme.PanelBg);
        Gfx.Border(sb, panel, Theme.Gold * 0.9f, 2);

        // Decorative top accent line
        Gfx.Rect(sb, _panelX + 1, _panelY + 1, PanelW - 2, 3, Theme.Gold * 0.5f);

        // Title
        const string title = "Paused";
        var titleSz = Assets.FontMedium.MeasureString(title);
        Gfx.Text(sb, Assets.FontMedium, title,
            new Vector2(_panelX + (PanelW - titleSz.X) / 2f, _panelY + 14),
            Theme.GoldSoft);

        // Divider under title
        int divY = _panelY + TitleH - 8;
        Gfx.Rect(sb, _panelX + PadX, divY, PanelW - PadX * 2, 1, Theme.Gold * 0.35f);

        // Buttons
        foreach (var btn in _buttons) btn.Draw(sb);

        // Footer hint
        const string hint = "ESC  Resume";
        var hintSz = Assets.FontSmall.MeasureString(hint);
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2(_panelX + (PanelW - hintSz.X) / 2f, _panelY + PanelH - FooterH + 10),
            Theme.ForegroundDim);
    }
}
