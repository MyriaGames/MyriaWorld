using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myria.Lib.Core.Entities;
using Myria.Lib.Core.Entities.Characters;
using Myria.Lib.Core.Systems.Enums;
using Myria.Mono.Services;
using Myria.Mono.UI;

namespace Myria.Mono.Screens;

/// <summary>SC1 — Race + class + name picker for a brand-new local character.</summary>
public class CharacterCreationScreen : Screen
{
    // ── Layout constants ──────────────────────────────────────────────────────
    private int _sw, _sh;

    // Panel
    private const int PanW = 1180, PanH = 640;
    private int _px, _py;

    // Column positions (relative to panel left)
    private const int Col1X =  20, Col1W = 250;   // Race
    private const int Col2X = 290, Col2W = 520;   // Class
    private const int Col3X = 830, Col3W = 330;   // Name + preview

    // ── Data ─────────────────────────────────────────────────────────────────
    private List<string> _races   = new();
    private List<string> _classes = new();

    private string _selectedRace  = "";
    private string _selectedClass = "";

    // ── UI ───────────────────────────────────────────────────────────────────
    private UiTextBox _nameBox = null!;
    private MouseState _prevMouse;
    private KeyboardState _prevKb;

    private string _error = "";

    // ── Class grid layout ─────────────────────────────────────────────────────
    private const int ClassCols = 3;
    private const int CardW = 163, CardH = 72, CardGap = 4;

    // ── Race card layout ──────────────────────────────────────────────────────
    private const int RaceCardH = 46, RaceCardGap = 4;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void LoadContent()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;
        _px = (_sw - PanW) / 2;
        _py = (_sh - PanH) / 2;

        // Load race and class data from their own JSON files.
        // These are safe to call before GameService.InitializeGame().
        RaceProfile.Load();
        ClassProfile.Load();

        _races   = RaceProfile.All.Keys.OrderBy(k => k).ToList();
        _classes = ClassProfile.All.Keys.OrderBy(k => k).ToList();

        // Default selections
        if (_races.Count   > 0) _selectedRace  = _races[0];
        if (_classes.Count > 0) _selectedClass = _classes[0];

        int nameBoxX = _px + Col3X + 10;
        int nameBoxY = _py + 64;
        _nameBox = new UiTextBox(new Rectangle(nameBoxX, nameBoxY, Col3W - 20, 40), Assets.FontNormal);

        _prevMouse = Mouse.GetState();
    }

    public override void OnEnter()
    {
        Game1.Instance.IsMouseVisible = true;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(GameTime gt)
    {
        var kb = Keyboard.GetState();
        var ms = Mouse.GetState();

        if (WasKeyJustPressed(kb, Keys.Escape))
        {
            ScreenManager.Instance.GoBack();
            _prevKb = kb;
            return;
        }

        _nameBox.Update(gt);

        bool clicked = ms.LeftButton == ButtonState.Released
                    && _prevMouse.LeftButton == ButtonState.Pressed;

        if (clicked)
        {
            // Hit-test race cards
            int raceCardX = _px + Col1X;
            int raceCardW = Col1W;
            int cy = _py + 72;
            foreach (var race in _races)
            {
                var r = new Rectangle(raceCardX, cy, raceCardW, RaceCardH);
                if (r.Contains(ms.Position)) { _selectedRace = race; _error = ""; }
                cy += RaceCardH + RaceCardGap;
            }

            // Hit-test class cards
            int clsStartX = _px + Col2X;
            int clsStartY = _py + 72;
            for (int idx = 0; idx < _classes.Count; idx++)
            {
                int col = idx % ClassCols;
                int row = idx / ClassCols;
                var r = new Rectangle(
                    clsStartX + col * (CardW + CardGap),
                    clsStartY + row * (CardH + CardGap),
                    CardW, CardH);
                if (r.Contains(ms.Position))
                {
                    var cls = _classes[idx];
                    if (!IsForbidden(_selectedRace, cls))
                    {
                        _selectedClass = cls;
                        _error = "";
                    }
                }
            }

            // Create button hit-test
            var createBtn = CreateButtonRect();
            if (createBtn.Contains(ms.Position))
                TryCreate();
        }

        _prevMouse = ms;
        _prevKb    = kb;
    }

    public override void OnTextInput(char c) => _nameBox.OnTextInput(c);

    // ── Draw ──────────────────────────────────────────────────────────────────

    public override void Draw(SpriteBatch sb)
    {
        ScreenManager.Instance.GraphicsDevice.Clear(Theme.Background);
        sb.Begin();

        // Panel background
        Gfx.Rect(sb, _px, _py, PanW, PanH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(_px, _py, PanW, PanH), Theme.Gold);

        // Title
        string title = "Create Your Character";
        var tSz = Assets.FontMedium.MeasureString(title);
        Gfx.Text(sb, Assets.FontMedium, title,
            new Vector2(_px + (PanW - tSz.X) / 2f, _py + 10), Theme.GoldSoft);

        // Column dividers
        int divY1 = _py + 48;
        int divH  = PanH - 48 - 10;
        Gfx.Rect(sb, _px + Col2X - 10, divY1, 1, divH, Theme.Gold * 0.3f);
        Gfx.Rect(sb, _px + Col3X - 10, divY1, 1, divH, Theme.Gold * 0.3f);

        DrawRaceColumn(sb);
        DrawClassColumn(sb);
        DrawPreviewColumn(sb);

        // ESC hint
        Gfx.Text(sb, Assets.FontSmall, "ESC — Back",
            new Vector2(_px + 12, _py + PanH - 22), Theme.ForegroundDim);

        sb.End();
    }

    private void DrawRaceColumn(SpriteBatch sb)
    {
        int cx = _px + Col1X;
        int cy = _py + 52;

        // Column header
        Gfx.Text(sb, Assets.FontNormal, "Race",
            new Vector2(cx, cy), Theme.ForegroundDim);
        cy += 20;

        foreach (var race in _races)
        {
            bool selected = race == _selectedRace;
            var r = new Rectangle(cx, cy, Col1W, RaceCardH);

            Gfx.Rect(sb, r, selected ? Theme.BlueActive : Theme.PanelDark);
            Gfx.Border(sb, r, selected ? Theme.Gold : Theme.Gold * 0.28f);

            if (selected)
                Gfx.Rect(sb, cx, cy, 4, RaceCardH, Theme.Gold);

            Gfx.Text(sb, Assets.FontNormal, race,
                new Vector2(cx + 12, cy + 8),
                selected ? Theme.GoldSoft : Theme.Foreground);

            // Brief stat summary on the same card
            if (RaceProfile.All.TryGetValue(race, out var prof))
            {
                string stats = $"HP+{prof.BaseHpBonus}  MP+{prof.BaseManaBonus}";
                Gfx.Text(sb, Assets.FontSmall, stats,
                    new Vector2(cx + 12, cy + 26), Theme.ForegroundDim);
            }

            cy += RaceCardH + RaceCardGap;
        }
    }

    private void DrawClassColumn(SpriteBatch sb)
    {
        int startX = _px + Col2X;
        int startY = _py + 52;

        // Column header
        Gfx.Text(sb, Assets.FontNormal, "Class",
            new Vector2(startX, startY), Theme.ForegroundDim);
        startY += 20;

        for (int idx = 0; idx < _classes.Count; idx++)
        {
            var cls      = _classes[idx];
            int col      = idx % ClassCols;
            int row      = idx / ClassCols;
            int cx       = startX + col * (CardW + CardGap);
            int cy       = startY + row * (CardH + CardGap);
            bool sel     = cls == _selectedClass;
            bool forb    = IsForbidden(_selectedRace, cls);

            float alpha  = forb ? 0.3f : 1f;
            var   r      = new Rectangle(cx, cy, CardW, CardH);

            Gfx.Rect(sb, r, (sel ? Theme.BlueActive : Theme.PanelDark) * alpha);
            Gfx.Border(sb, r, (sel ? Theme.Gold : Theme.Gold * 0.28f) * alpha);

            if (sel && !forb)
                Gfx.Rect(sb, cx, cy, 4, CardH, Theme.Gold);

            Gfx.Text(sb, Assets.FontNormal, DisplayClass(cls),
                new Vector2(cx + 10, cy + 8),
                (sel ? Theme.GoldSoft : Theme.Foreground) * alpha);

            if (ClassProfile.All.TryGetValue(cls, out var prof))
            {
                string growth = $"HP/lv +{prof.HpPerLevel}   MP/lv +{prof.ManaPerLevel}";
                Gfx.Text(sb, Assets.FontSmall, growth,
                    new Vector2(cx + 10, cy + 32), Theme.ForegroundDim * alpha);
            }

            if (forb)
            {
                string locked = "Restricted";
                var lSz = Assets.FontSmall.MeasureString(locked);
                Gfx.Text(sb, Assets.FontSmall, locked,
                    new Vector2(cx + (CardW - lSz.X) / 2f, cy + 50),
                    new Color(200, 80, 80) * 0.7f);
            }
        }
    }

    private void DrawPreviewColumn(SpriteBatch sb)
    {
        int cx = _px + Col3X + 10;
        int cy = _py + 52;
        int cw = Col3W - 20;

        // Column header
        Gfx.Text(sb, Assets.FontNormal, "Character",
            new Vector2(cx, cy), Theme.ForegroundDim);
        cy += 20;

        // Name label + text box
        Gfx.Text(sb, Assets.FontSmall, "Name",
            new Vector2(cx, cy), Theme.ForegroundDim);
        cy += 16;
        _nameBox.Draw(sb);
        cy += 48;

        // Stat preview
        if (!string.IsNullOrEmpty(_selectedRace) && RaceProfile.All.TryGetValue(_selectedRace, out var rp))
        {
            Gfx.Rect(sb, cx, cy, cw, 1, Theme.Gold * 0.3f);
            cy += 6;
            Gfx.Text(sb, Assets.FontSmall, "Base Stats", new Vector2(cx, cy), Theme.ForegroundDim);
            cy += 18;

            (string key, string label)[] statRows =
            {
                ("STR", "Strength"),
                ("DEX", "Dexterity"),
                ("END", "Endurance"),
                ("INT", "Intelligence"),
                ("SPR", "Spirit"),
            };
            foreach (var (key, label) in statRows)
            {
                int bonus = rp.BaseStatBonus.GetValueOrDefault(key);
                int total = 10 + bonus;
                Color col = bonus > 0 ? new Color(100, 200, 100)
                           : bonus < 0 ? new Color(200, 100, 100)
                           : Theme.Foreground;
                Gfx.Text(sb, Assets.FontSmall, $"{label,-12} {total,2}",
                    new Vector2(cx, cy), col);
                cy += 18;
            }

            cy += 4;
            Gfx.Text(sb, Assets.FontSmall,
                $"Base HP   {100 + rp.BaseHpBonus}",
                new Vector2(cx, cy), Theme.Foreground);
            cy += 18;
            Gfx.Text(sb, Assets.FontSmall,
                $"Base MP   {30 + rp.BaseManaBonus}",
                new Vector2(cx, cy), Theme.Foreground);
            cy += 26;

            // Per-level growth from class
            if (!string.IsNullOrEmpty(_selectedClass) && ClassProfile.All.TryGetValue(_selectedClass, out var cp))
            {
                Gfx.Rect(sb, cx, cy, cw, 1, Theme.Gold * 0.3f);
                cy += 6;
                Gfx.Text(sb, Assets.FontSmall, "Growth per Level", new Vector2(cx, cy), Theme.ForegroundDim);
                cy += 18;
                Gfx.Text(sb, Assets.FontSmall,
                    $"HP +{cp.HpPerLevel}   MP +{cp.ManaPerLevel}",
                    new Vector2(cx, cy), Theme.Foreground);
                cy += 18;

                string growth = string.Join("  ", cp.StatGrowth
                    .Where(kv => kv.Value > 0)
                    .Select(kv => $"{kv.Key}+{kv.Value}"));
                if (growth.Length > 0)
                    Gfx.Text(sb, Assets.FontSmall, growth, new Vector2(cx, cy), Theme.ForegroundDim);
            }
        }

        // Error message
        if (_error.Length > 0)
        {
            var eSz = Assets.FontSmall.MeasureString(_error);
            Gfx.Text(sb, Assets.FontSmall, _error,
                new Vector2(cx + (cw - eSz.X) / 2f, _py + PanH - 80),
                new Color(210, 80, 80));
        }

        // Create button
        var btn = CreateButtonRect();
        bool canCreate = _nameBox.Text.Trim().Length > 0
                      && _selectedRace.Length > 0
                      && _selectedClass.Length > 0
                      && !IsForbidden(_selectedRace, _selectedClass);

        Gfx.Rect(sb, btn, canCreate ? Theme.BlueActive : Theme.PanelDark);
        Gfx.Border(sb, btn, canCreate ? Theme.Gold : Theme.Gold * 0.25f);
        Gfx.TextCentered(sb, Assets.FontNormal, "Create Character", btn,
            canCreate ? Theme.GoldSoft : Theme.ForegroundDim * 0.5f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Rectangle CreateButtonRect() =>
        new(_px + Col3X + 10, _py + PanH - 64, Col3W - 20, 50);

    private bool IsForbidden(string race, string cls)
    {
        if (!RaceProfile.All.TryGetValue(race, out var rp)) return false;
        return rp.ForbiddenClasses.Contains(cls);
    }

    private static string DisplayClass(string cls) => cls switch
    {
        "ElementalMage" => "Elem. Mage",
        "ArcanMage"     => "Arcan Mage",
        "SoulsKnight"   => "Souls Knight",
        "RunicMage"     => "Runic Mage",
        _               => cls,
    };

    private void TryCreate()
    {
        string name = _nameBox.Text.Trim();

        if (name.Length == 0)       { _error = "Enter a name."; return; }
        if (name.Length > 18)       { _error = "Name too long (max 18)."; return; }
        if (_selectedRace.Length == 0)  { _error = "Select a race.";  return; }
        if (_selectedClass.Length == 0) { _error = "Select a class."; return; }
        if (IsForbidden(_selectedRace, _selectedClass))
        {
            _error = $"{_selectedClass} is restricted for {_selectedRace}.";
            return;
        }
        if (LocalSaveService.NameExists(name))
        {
            _error = $"A character named '{name}' already exists.";
            return;
        }

        // Build the starter character
        var rp = RaceProfile.All[_selectedRace];
        var stats = new Stats
        {
            Strength     = 10 + rp.BaseStatBonus.GetValueOrDefault("STR"),
            Dexterity    = 10 + rp.BaseStatBonus.GetValueOrDefault("DEX"),
            Endurance    = 10 + rp.BaseStatBonus.GetValueOrDefault("END"),
            Intelligence = 10 + rp.BaseStatBonus.GetValueOrDefault("INT"),
            Spirit       = 10 + rp.BaseStatBonus.GetValueOrDefault("SPR"),
            BaseHealth   = 100 + rp.BaseHpBonus,
            BaseMana     = 30  + rp.BaseManaBonus,
        };
        var character = new Character(name, stats)
        {
            Race          = _selectedRace,
            Class         = _selectedClass,
            Level         = 1,
            RaceSelected  = true,
        };
        character.CurrentHealth = character.MaxHealth > 0 ? character.MaxHealth : stats.BaseHealth;
        character.CurrentMana   = character.MaxMana   > 0 ? character.MaxMana   : stats.BaseMana;

        // LoadingScreen will load full game data, update skills, then save the character
        ScreenManager.Instance.Navigate(new LoadingScreen(character, saveOnLoad: true));
    }

    private bool WasKeyJustPressed(KeyboardState cur, Keys key)
        => cur.IsKeyDown(key) && !_prevKb.IsKeyDown(key);
}
