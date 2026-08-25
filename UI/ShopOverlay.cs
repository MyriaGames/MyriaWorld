using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myria.Lib.Core.Entities.Characters;
using Myria.Lib.Core.Entities.Items;
using Myria.Lib.Core.Entities.NPCs;
using Myria.Mono.Screens;
using Myria.Mono.Services;

namespace Myria.Mono.UI;

public sealed class ShopOverlay
{
    public bool IsOpen { get; private set; }

    private Npc?       _npc;
    private Character? _player;
    private int        _hoveredIdx = -1;
    private string     _feedback   = "";
    private float      _feedbackTimer;
    private const float FeedbackTime = 2f;

    private const int PanelW  = 520;
    private const int RowH    = 44;
    private const int PadX    = 18;
    private const int TitleH  = 48;
    private const int FooterH = 36;
    private const int MaxRows = 8;

    public void Open(Npc npc, Character player)
    {
        _npc       = npc;
        _player    = player;
        _hoveredIdx = -1;
        _feedback  = "";
        IsOpen     = true;
    }

    public void Close() => IsOpen = false;

    public void Update(MouseState ms, MouseState prevMs)
    {
        if (!IsOpen || _npc == null || _player == null) return;

        var gd = ScreenManager.Instance.GraphicsDevice;
        int sw = gd.Viewport.Width, sh = gd.Viewport.Height;

        int rows    = Math.Min(_npc.ItemRefs.Count, MaxRows);
        int panelH  = TitleH + rows * RowH + FooterH;
        int px      = (sw - PanelW) / 2;
        int py      = (sh - panelH) / 2;

        _hoveredIdx = -1;
        for (int i = 0; i < rows; i++)
        {
            var row = new Rectangle(px + PadX, py + TitleH + i * RowH, PanelW - PadX * 2, RowH - 2);
            if (row.Contains(ms.X, ms.Y))
            {
                _hoveredIdx = i;
                if (ms.LeftButton == ButtonState.Released && prevMs.LeftButton == ButtonState.Pressed)
                    TryBuy(i);
            }
        }

        if (_feedbackTimer > 0f) _feedbackTimer -= (float)0.016;  // approximation; WorldScreen passes dt
    }

    public void UpdateDt(float dt) { if (_feedbackTimer > 0f) _feedbackTimer -= dt; }

    private void TryBuy(int idx)
    {
        if (_npc == null || _player == null) return;
        var item = _npc.ItemRefs[idx];
        var result = _npc.BuyItem(_player, item, 1);
        _feedback      = result.Success
            ? $"Bought {item.Name}!"
            : WorldDataService.Localize(result.MessageKey);
        _feedbackTimer = FeedbackTime;
    }

    public void Draw(SpriteBatch sb)
    {
        if (!IsOpen || _npc == null || _player == null) return;

        var gd = ScreenManager.Instance.GraphicsDevice;
        int sw = gd.Viewport.Width, sh = gd.Viewport.Height;

        int rows   = Math.Min(_npc.ItemRefs.Count, MaxRows);
        int panelH = TitleH + rows * RowH + FooterH;
        int px     = (sw - PanelW) / 2;
        int py     = (sh - panelH) / 2;

        // Dim background
        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, 160));

        Gfx.Rect(sb, px, py, PanelW, panelH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, PanelW, panelH), Theme.Gold * 0.85f, 2);

        // Title + gold
        Gfx.Text(sb, Assets.FontMedium, _npc.ToString(),
            new Vector2(px + PadX, py + 10), Theme.GoldSoft);
        string goldStr = $"{_player.Money.Balance.BronzeTotal} Gold";
        var gSz = Assets.FontSmall.MeasureString(goldStr);
        Gfx.Text(sb, Assets.FontSmall, goldStr,
            new Vector2(px + PanelW - gSz.X - PadX, py + 16),
            new Color(220, 185, 60));

        Gfx.Rect(sb, px + PadX, py + TitleH - 4, PanelW - PadX * 2, 1, Theme.Gold * 0.35f);

        // Item rows
        if (rows == 0)
        {
            Gfx.Text(sb, Assets.FontNormal, "No items available.",
                new Vector2(px + PadX, py + TitleH + 8), Theme.ForegroundDim);
        }

        for (int i = 0; i < rows; i++)
        {
            var item    = _npc.ItemRefs[i];
            bool hovered = i == _hoveredIdx;
            int  ry     = py + TitleH + i * RowH;

            Gfx.Rect(sb, px + PadX, ry, PanelW - PadX * 2, RowH - 2,
                hovered ? Theme.PanelDark * 1.6f : Theme.PanelDark);

            Color nameCol = RarityColor(item.Rarity);
            Gfx.Text(sb, Assets.FontNormal, item.Name,
                new Vector2(px + PadX + 8, ry + 10), nameCol);

            string price = $"{item.BuyPrice} G";
            var pSz = Assets.FontSmall.MeasureString(price);
            Gfx.Text(sb, Assets.FontSmall, price,
                new Vector2(px + PanelW - pSz.X - PadX - 8, ry + 13),
                new Color(220, 185, 60));
        }

        // Feedback + footer
        if (_feedbackTimer > 0f && _feedback.Length > 0)
        {
            float alpha = Math.Clamp(_feedbackTimer, 0f, 1f);
            Gfx.Text(sb, Assets.FontSmall, _feedback,
                new Vector2(px + PadX, py + panelH - FooterH + 4),
                new Color(120, 220, 120) * alpha);
        }

        var closeSz = Assets.FontSmall.MeasureString("ESC  Close");
        Gfx.Text(sb, Assets.FontSmall, "ESC  Close",
            new Vector2(px + PanelW - closeSz.X - PadX, py + panelH - FooterH + 4),
            Theme.ForegroundDim);
    }

    private static Color RarityColor(string r) => r switch
    {
        Myria.Lib.Core.Systems.Enums.ItemRarity.Uncommon  => new Color(30,  200, 80),
        Myria.Lib.Core.Systems.Enums.ItemRarity.Rare      => new Color(60,  120, 220),
        Myria.Lib.Core.Systems.Enums.ItemRarity.Epic      => new Color(160, 60,  220),
        Myria.Lib.Core.Systems.Enums.ItemRarity.Legendary => new Color(220, 140, 30),
        _                                            => Theme.Foreground,
    };
}
