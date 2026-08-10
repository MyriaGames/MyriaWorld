using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaLib.Entities.NPCs;
using Myria.Mono.Services;

namespace Myria.Mono.UI;

public enum DialogueMode { Regular, QuestAccept, QuestReturn }

public sealed class DialogueOverlay
{
    public bool         IsOpen { get; private set; }
    public DialogueMode Mode   { get; private set; }

    // Regular dialogue
    private string   _npcName = "";
    private string[] _lines   = [];

    // Quest data (used in QuestAccept / QuestReturn modes)
    public  Quest?   PendingQuest { get; private set; }

    private const int PanelW  = 500;
    private const int LineH   = 22;
    private const int PadX    = 20;
    private const int PadY    = 14;
    private const int TitleH  = 42;
    private const int FooterH = 32;

    // ── Open helpers ──────────────────────────────────────────────────────────

    public void Open(string npcName, string[] lines)
    {
        _npcName    = npcName;
        _lines      = lines;
        PendingQuest = null;
        Mode         = DialogueMode.Regular;
        IsOpen       = true;
    }

    public void OpenQuestAccept(string npcName, Quest quest)
    {
        _npcName     = npcName;
        PendingQuest = quest;
        Mode         = DialogueMode.QuestAccept;
        _lines       = BuildQuestAcceptLines(quest);
        IsOpen       = true;
    }

    public void OpenQuestReturn(string npcName, Quest quest)
    {
        _npcName     = npcName;
        PendingQuest = quest;
        Mode         = DialogueMode.QuestReturn;
        _lines       = BuildQuestReturnLines(quest);
        IsOpen       = true;
    }

    public void Close()
    {
        IsOpen       = false;
        PendingQuest = null;
    }

    // ── Draw ─────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, int sw, int sh)
    {
        if (!IsOpen) return;

        int panelH = TitleH + _lines.Length * LineH + PadY + FooterH;
        int px     = (sw - PanelW) / 2;
        int py     = sh - panelH - 80;

        Gfx.Rect(sb, px, py, PanelW, panelH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, PanelW, panelH), Theme.Gold * 0.85f, 2);

        // NPC name + mode badge
        string badge = Mode switch
        {
            DialogueMode.QuestAccept => "  [Quest]",
            DialogueMode.QuestReturn => "  [Return]",
            _                        => "",
        };
        Gfx.Text(sb, Assets.FontMedium, _npcName + badge,
            new Vector2(px + PadX, py + 10), Theme.GoldSoft);

        Gfx.Rect(sb, px + PadX, py + TitleH - 4, PanelW - PadX * 2, 1, Theme.Gold * 0.4f);

        // Body lines
        for (int i = 0; i < _lines.Length; i++)
            Gfx.Text(sb, Assets.FontSmall, _lines[i],
                new Vector2(px + PadX, py + TitleH + i * LineH + 4), Theme.Foreground);

        // Footer hint
        string hint = Mode switch
        {
            DialogueMode.QuestAccept => "Y  Accept   ESC  Decline",
            DialogueMode.QuestReturn => "F / ESC  Close",
            _                        => "F / ESC  Close",
        };
        var hSz = Assets.FontSmall.MeasureString(hint);
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2(px + PanelW - hSz.X - PadX, py + panelH - FooterH + 10),
            Theme.ForegroundDim);
    }

    // ── Line builders ─────────────────────────────────────────────────────────

    private static string[] BuildQuestAcceptLines(Quest q)
    {
        var lines = new List<string>();
        string name = WorldDataService.Localize(q.Name);
        string desc = WordWrap(WorldDataService.Localize(q.Description), 68);
        lines.Add(name);
        lines.Add("");
        foreach (var l in desc.Split('\n')) lines.Add("  " + l);
        lines.Add("");
        lines.Add(BuildObjectiveSummary(q));
        string reward = $"Reward: {q.RewardXp} XP" + (q.RewardGold > 0 ? $"  {q.RewardGold} Gold" : "");
        lines.Add(reward);
        return [.. lines];
    }

    private static string[] BuildQuestReturnLines(Quest q)
    {
        var firstReturn = q.ReturnDialog.FirstOrDefault();
        string text = firstReturn != null
            ? WorldDataService.Localize(firstReturn.Text)
            : "Quest complete!";
        string reward = $"Reward: {q.RewardXp} XP" + (q.RewardGold > 0 ? $"  {q.RewardGold} Gold" : "");
        return [WorldDataService.Localize(q.Name), "", "  " + text, "", reward];
    }

    private static string BuildObjectiveSummary(Quest q)
    {
        if (q.RequiredKills.Count == 0 && q.RequiredItems.Count == 0)
            return "Objective: Speak and return.";
        var parts = new List<string>();
        foreach (var (_, amt) in q.RequiredKills)  parts.Add($"Kill x{amt}");
        foreach (var (id, amt) in q.RequiredItems) parts.Add($"Collect {id} x{amt}");
        return "Objective: " + string.Join(", ", parts);
    }

    private static string WordWrap(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        int cut = text.LastIndexOf(' ', maxChars);
        if (cut <= 0) cut = maxChars;
        return text[..cut] + "\n" + WordWrap(text[(cut + 1)..], maxChars);
    }
}
