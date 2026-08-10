using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaLib.Entities.Characters;
using MyriaLib.Entities.NPCs;
using MyriaLib.Services;
using MyriaLib.Systems.Enums;
using Myria.Mono.Services;

namespace Myria.Mono.UI;

public sealed class QuestLogOverlay
{
    public bool IsOpen { get; private set; }

    private Character?   _player;
    private int          _scroll;
    private int          _selectedIdx = -1;

    private const int PanelW  = 680;
    private const int PanelH  = 460;
    private const int ListW   = 210;
    private const int PadX    = 14;
    private const int EntryH  = 36;
    private const int TitleH  = 46;
    private const int FooterH = 32;

    public void Open(Character player)
    {
        _player      = player;
        _scroll      = 0;
        _selectedIdx = player.ActiveQuests.Count > 0 ? 0 : -1;
        IsOpen       = true;
    }

    public void Close() => IsOpen = false;

    // Returns the quest the user wants to track, or null if no change was requested.
    public Quest? ConsumeTrackRequest()
    {
        if (_trackRequest == null) return null;
        var r = _trackRequest;
        _trackRequest = null;
        return r;
    }
    private Quest? _trackRequest;

    public void Update(MouseState ms, MouseState prevMs)
    {
        if (!IsOpen || _player == null) return;

        var gd = Myria.Mono.Screens.ScreenManager.Instance.GraphicsDevice;
        int sw = gd.Viewport.Width, sh = gd.Viewport.Height;
        int px = (sw - PanelW) / 2;
        int py = (sh - PanelH) / 2;

        bool clicked = ms.LeftButton == ButtonState.Released && prevMs.LeftButton == ButtonState.Pressed;

        int listCount = _player.ActiveQuests.Count;
        int maxVisible = (PanelH - TitleH - FooterH) / EntryH;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, listCount - maxVisible));

        for (int i = 0; i < Math.Min(listCount, maxVisible); i++)
        {
            int qi = i + _scroll;
            var row = new Rectangle(px + 2, py + TitleH + i * EntryH, ListW - 4, EntryH - 2);
            if (row.Contains(ms.X, ms.Y) && clicked)
            {
                _selectedIdx = qi;
                if (qi < _player.ActiveQuests.Count)
                    _trackRequest = _player.ActiveQuests[qi];
            }
        }

        // Mouse wheel scroll on list
        int wheel = ms.ScrollWheelValue - prevMs.ScrollWheelValue;
        if (wheel != 0) _scroll = Math.Clamp(_scroll - wheel / 120, 0, Math.Max(0, listCount - maxVisible));
    }

    public void Draw(SpriteBatch sb)
    {
        if (!IsOpen || _player == null) return;

        var gd = Myria.Mono.Screens.ScreenManager.Instance.GraphicsDevice;
        int sw = gd.Viewport.Width, sh = gd.Viewport.Height;

        // Full-screen dim
        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, 170));

        int px = (sw - PanelW) / 2;
        int py = (sh - PanelH) / 2;

        Gfx.Rect(sb, px, py, PanelW, PanelH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, PanelW, PanelH), Theme.Gold * 0.85f, 2);

        // Title
        Gfx.Text(sb, Assets.FontMedium, "Quest Log",
            new Vector2(px + PadX, py + 10), Theme.GoldSoft);
        string countStr = $"{_player.ActiveQuests.Count} active  {_player.CompletedQuests.Count} completed";
        var cSz = Assets.FontSmall.MeasureString(countStr);
        Gfx.Text(sb, Assets.FontSmall, countStr,
            new Vector2(px + PanelW - cSz.X - PadX, py + 16), Theme.ForegroundDim);

        Gfx.Rect(sb, px + PadX, py + TitleH - 6, PanelW - PadX * 2, 1, Theme.Gold * 0.35f);

        // Split: left = quest list, right = detail
        int listH   = PanelH - TitleH - FooterH;
        int detailX = px + ListW + 6;
        int detailW = PanelW - ListW - 6 - PadX;

        // Vertical divider
        Gfx.Rect(sb, px + ListW + 2, py + TitleH, 1, listH, Theme.Gold * 0.2f);

        DrawQuestList(sb, px, py + TitleH, listH);
        DrawQuestDetail(sb, detailX, py + TitleH, detailW, listH);

        // Footer
        Gfx.Rect(sb, px + PadX, py + PanelH - FooterH, PanelW - PadX * 2, 1, Theme.Gold * 0.2f);
        string hint = "Q / ESC  Close   Click quest to track";
        var hSz = Assets.FontSmall.MeasureString(hint);
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2(px + (PanelW - hSz.X) / 2f, py + PanelH - FooterH + 8), Theme.ForegroundDim);
    }

    // ── Left panel: quest list ────────────────────────────────────────────────

    private void DrawQuestList(SpriteBatch sb, int px, int py, int listH)
    {
        if (_player == null) return;

        int maxVisible = listH / EntryH;
        int listCount  = _player.ActiveQuests.Count;

        for (int i = 0; i < Math.Min(listCount, maxVisible); i++)
        {
            int   qi    = i + _scroll;
            var   quest = _player.ActiveQuests[qi];
            int   ry    = py + i * EntryH;
            bool  sel   = qi == _selectedIdx;

            Color bg = sel ? Theme.PanelDark * 1.8f : Theme.PanelDark;
            Gfx.Rect(sb, px + 2, ry, ListW - 4, EntryH - 2, bg);

            string name = WorldDataService.Localize(quest.Name);
            if (name.Length > 22) name = name[..22] + "..";

            Color readyCol = quest.Status == QuestStatus.Completed
                ? new Color(120, 220, 80) : Theme.GoldSoft;
            Gfx.Text(sb, Assets.FontSmall, name,
                new Vector2(px + 6, ry + 4), readyCol);

            string status = quest.Status == QuestStatus.Completed ? "Ready!" : "Active";
            Gfx.Text(sb, Assets.FontSmall, status,
                new Vector2(px + 6, ry + 19), Theme.ForegroundDim);
        }

        // Scroll indicator
        if (listCount > maxVisible)
        {
            string sc = $"{_scroll + 1}-{Math.Min(_scroll + maxVisible, listCount)}/{listCount}";
            var scSz = Assets.FontSmall.MeasureString(sc);
            Gfx.Text(sb, Assets.FontSmall, sc,
                new Vector2(px + (ListW - scSz.X) / 2f, py + listH - 18), Theme.ForegroundDim);
        }
    }

    // ── Right panel: quest detail ─────────────────────────────────────────────

    private void DrawQuestDetail(SpriteBatch sb, int dx, int dy, int dw, int dh)
    {
        if (_player == null || _selectedIdx < 0 || _selectedIdx >= _player.ActiveQuests.Count)
        {
            Gfx.Text(sb, Assets.FontSmall, "Select a quest to view details.",
                new Vector2(dx + 8, dy + 12), Theme.ForegroundDim);
            return;
        }

        var q   = _player.ActiveQuests[_selectedIdx];
        int cy  = dy + 8;

        // Quest name
        string name = WorldDataService.Localize(q.Name);
        Color nameCol = q.Status == QuestStatus.Completed
            ? new Color(120, 220, 80) : Theme.GoldSoft;
        Gfx.Text(sb, Assets.FontMedium, name, new Vector2(dx, cy), nameCol);
        cy += 28;

        // Description (wrapped)
        string desc = WorldDataService.Localize(q.Description);
        foreach (var line in WrapLines(desc, dw - 16, Assets.FontSmall))
        {
            Gfx.Text(sb, Assets.FontSmall, line, new Vector2(dx + 4, cy), Theme.Foreground);
            cy += 18;
        }
        cy += 6;

        // Objectives header
        Gfx.Rect(sb, dx, cy, dw - 4, 1, Theme.Gold * 0.25f);
        cy += 4;
        Gfx.Text(sb, Assets.FontSmall, "Objectives:", new Vector2(dx + 4, cy), Theme.ForegroundDim);
        cy += 20;

        bool hasObjectives = false;

        // Kill objectives
        foreach (var (monsterId, required) in q.RequiredKills)
        {
            q.KillProgress.TryGetValue(monsterId, out int got);
            bool done = got >= required;

            string mobName = GetMonsterName(monsterId);
            string line    = $"  Kill {mobName}: {got}/{required}";
            Color  lineCol = done ? new Color(120, 220, 80) : Theme.Foreground;
            Gfx.Text(sb, Assets.FontSmall, line, new Vector2(dx + 4, cy), lineCol);
            cy += 18;
            hasObjectives = true;
        }

        // Item objectives
        foreach (var (itemId, required) in q.RequiredItems)
        {
            string line   = $"  Collect {itemId} (x{required})";
            Gfx.Text(sb, Assets.FontSmall, line, new Vector2(dx + 4, cy), Theme.ForegroundDim);
            cy += 18;
            hasObjectives = true;
        }

        if (!hasObjectives)
        {
            Gfx.Text(sb, Assets.FontSmall, "  Speak with the quest giver.",
                new Vector2(dx + 4, cy), Theme.ForegroundDim);
            cy += 18;
        }

        if (q.Status == QuestStatus.Completed)
        {
            cy += 4;
            Gfx.Text(sb, Assets.FontSmall, ">> Return to quest giver to claim rewards!",
                new Vector2(dx + 4, cy), new Color(120, 220, 80));
            cy += 20;
        }

        // Rewards
        cy += 6;
        Gfx.Rect(sb, dx, cy, dw - 4, 1, Theme.Gold * 0.25f);
        cy += 4;
        Gfx.Text(sb, Assets.FontSmall, "Rewards:", new Vector2(dx + 4, cy), Theme.ForegroundDim);
        cy += 20;

        string rewards = $"  {q.RewardXp} XP";
        if (q.RewardGold > 0) rewards += $"   {q.RewardGold} Gold";
        Gfx.Text(sb, Assets.FontSmall, rewards, new Vector2(dx + 4, cy), new Color(220, 185, 60));
        cy += 18;

        foreach (var itemId in q.RewardItems)
        {
            Gfx.Text(sb, Assets.FontSmall, $"  + {itemId}", new Vector2(dx + 4, cy), Theme.Foreground);
            cy += 16;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetMonsterName(int monsterId)
    {
        var m = MonsterService.GetMonsterById(monsterId);
        return m != null ? WorldDataService.Localize(m.Name) : $"#{monsterId}";
    }

    private static IEnumerable<string> WrapLines(string text, int maxPx, SpriteFont font)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var words = text.Split(' ');
        string current = "";
        foreach (var word in words)
        {
            string test = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureString(test).X > maxPx && current.Length > 0)
            {
                yield return current;
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (current.Length > 0) yield return current;
    }
}
