using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myria.Lib.Core.Entities.Characters;
using Myria.Lib.Core.Entities.Items;
using Myria.Lib.Core.Systems.Enums;
using Myria.Mono.Screens;

namespace Myria.Mono.UI;

public sealed class InventoryOverlay
{
    private readonly Character _player;
    private Item? _hoveredItem;

    private const int SlotSize = 70;
    private const int SlotGap  = 4;
    private const int Cols     = 7;
    private const int PadX     = 20;
    private const int TitleH   = 50;

    public InventoryOverlay(Character player) => _player = player;

    public void Draw(SpriteBatch sb, int mouseX, int mouseY)
    {
        var gd = ScreenManager.Instance.GraphicsDevice;
        int sw = gd.Viewport.Width;
        int sh = gd.Viewport.Height;

        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, 180));

        int capacity = _player.Inventory.Capacity;
        int rows     = (int)Math.Ceiling(capacity / (float)Cols);
        int panelW   = Cols * (SlotSize + SlotGap) - SlotGap + PadX * 2;
        int panelH   = TitleH + rows * (SlotSize + SlotGap) + PadX;
        int panelX   = (sw - panelW) / 2;
        int panelY   = (sh - panelH) / 2;

        Gfx.Rect(sb, panelX, panelY, panelW, panelH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(panelX, panelY, panelW, panelH), Theme.Gold * 0.8f);

        // Title row
        string title = $"Inventory  ({_player.Inventory.Items.Count}/{capacity})";
        Gfx.Text(sb, Assets.FontMedium, title, new Vector2(panelX + PadX, panelY + 10), Theme.GoldSoft);

        var closeSz = Assets.FontSmall.MeasureString("I / ESC  Close");
        Gfx.Text(sb, Assets.FontSmall, "I / ESC  Close",
            new Vector2(panelX + panelW - closeSz.X - 10, panelY + 16), Theme.ForegroundDim);

        // Gold
        string goldStr = $"Gold: {_player.Money.Balance.BronzeTotal}";
        Gfx.Text(sb, Assets.FontSmall, goldStr,
            new Vector2(panelX + PadX, panelY + TitleH - 16), new Color(220, 185, 60));

        // Item grid
        _hoveredItem = null;
        var items  = _player.Inventory.Items;
        int startY = panelY + TitleH;

        for (int i = 0; i < capacity; i++)
        {
            int col = i % Cols;
            int row = i / Cols;
            int sx  = panelX + PadX + col * (SlotSize + SlotGap);
            int sy  = startY + row * (SlotSize + SlotGap);
            var slotRect = new Rectangle(sx, sy, SlotSize, SlotSize);

            Item? item    = i < items.Count ? items[i] : null;
            bool  hovered = slotRect.Contains(mouseX, mouseY);
            if (hovered && item != null) _hoveredItem = item;

            Gfx.Rect(sb, sx, sy, SlotSize, SlotSize, hovered ? Theme.PanelDark * 1.5f : Theme.PanelDark);
            Gfx.Border(sb, slotRect, hovered ? Theme.Gold * 0.9f : Theme.Gold * 0.25f);

            if (item == null) continue;

            Color  nameColor = RarityColor(item.Rarity);
            string name      = item.Name.Length > 9 ? item.Name[..9] : item.Name;
            var    nameSz    = Assets.FontSmall.MeasureString(name);
            Gfx.Text(sb, Assets.FontSmall, name,
                new Vector2(sx + (SlotSize - nameSz.X) / 2f, sy + 24), nameColor);

            if (item.StackSize > 1)
                Gfx.Text(sb, Assets.FontSmall, $"x{item.StackSize}",
                    new Vector2(sx + 4, sy + SlotSize - 16), Theme.ForegroundDim);

            // Rarity dot (top-right corner of slot)
            Gfx.Rect(sb, sx + SlotSize - 10, sy + 4, 6, 6, nameColor);
        }

        if (_hoveredItem != null)
            DrawTooltip(sb, _hoveredItem, mouseX, mouseY, sw, sh);
    }

    private static void DrawTooltip(SpriteBatch sb, Item item, int mx, int my, int sw, int sh)
    {
        const int w = 220, h = 84;
        int tx = Math.Min(mx + 14, sw - w - 4);
        int ty = Math.Min(my + 14, sh - h - 4);

        Gfx.Rect(sb, tx, ty, w, h, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(tx, ty, w, h), RarityColor(item.Rarity) * 0.8f);

        Gfx.Text(sb, Assets.FontNormal, item.Name,            new Vector2(tx + 8, ty + 8),  RarityColor(item.Rarity));
        Gfx.Text(sb, Assets.FontSmall,  item.Rarity.ToString(), new Vector2(tx + 8, ty + 34), Theme.ForegroundDim);

        if (!string.IsNullOrEmpty(item.Description))
        {
            string desc = item.Description.Length > 30 ? item.Description[..30] + "..." : item.Description;
            Gfx.Text(sb, Assets.FontSmall, desc, new Vector2(tx + 8, ty + 56), Theme.Foreground);
        }
    }

    private static Color RarityColor(ItemRarity r) => r switch
    {
        ItemRarity.Uncommon  => new Color(30,  200, 80),
        ItemRarity.Rare      => new Color(60,  120, 220),
        ItemRarity.Epic      => new Color(160, 60,  220),
        ItemRarity.Legendary => new Color(220, 140, 30),
        _                    => Theme.Foreground,
    };
}
