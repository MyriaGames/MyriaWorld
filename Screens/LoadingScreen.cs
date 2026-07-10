using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaLib.Entities.Characters;
using MyriaWorld.Services;
using MyriaWorld.UI;

namespace MyriaWorld.Screens;

public class LoadingScreen : Screen
{
    // ── Mode A: pre-built character (debug or SC1 new-character path) ─────────
    private readonly Character? _playerArg;
    private readonly bool       _saveOnLoad;   // true → save after data is ready (SC1)

    // ── Mode B: load from file (SC2) ─────────────────────────────────────────
    private readonly string? _loadName;

    // ── Resolved after background task ────────────────────────────────────────
    private Character? _player;

    private volatile bool   _done;
    private volatile bool   _failed;
    private string          _errorMessage = "";

    private float _spinnerAngle;
    private int   _sw, _sh;

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>Debug path — plays with a pre-built character without saving.</summary>
    public LoadingScreen(Character player)
    {
        _playerArg  = player;
        _saveOnLoad = false;
    }

    /// <summary>SC1 path — plays with a brand-new character and saves it.</summary>
    public LoadingScreen(Character player, bool saveOnLoad)
    {
        _playerArg  = player;
        _saveOnLoad = saveOnLoad;
    }

    /// <summary>SC2 path — loads a character by name after game data is ready.</summary>
    public LoadingScreen(string characterName)
    {
        _loadName = characterName;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void LoadContent()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;
    }

    public override void OnEnter()
    {
        if (WorldDataService.IsLoaded && _loadName == null)
        {
            // Game data already loaded (e.g. re-entering from world) — skip background task
            _player = _playerArg!;
            if (_saveOnLoad) LocalSaveService.Save(_player);
            _done = true;
            return;
        }

        Task.Run(() =>
        {
            try
            {
                WorldDataService.Load();

                if (_loadName != null)
                {
                    // SC2: load character from file now that data is ready
                    _player = LocalSaveService.Load(_loadName);
                    if (_player == null)
                    {
                        _errorMessage = $"Could not load '{_loadName}'. Save file may be corrupt.";
                        _failed = true;
                        return;
                    }
                }
                else
                {
                    // Debug / SC1: use the pre-built character
                    _player = _playerArg!;
                    WorldDataService.PrepareCharacter(_player);
                    if (_saveOnLoad) LocalSaveService.Save(_player);
                }

                _done = true;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                _failed = true;
            }
        });
    }

    public override void Update(GameTime gt)
    {
        _spinnerAngle += (float)gt.ElapsedGameTime.TotalSeconds * 2.5f;

        if (_done)
        {
            ScreenManager.Instance.NavigateReplace(new WorldScreen(_player!));
            return;
        }

        if (_failed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            ScreenManager.Instance.GoBack();
    }

    public override void Draw(SpriteBatch sb)
    {
        ScreenManager.Instance.GraphicsDevice.Clear(Theme.Background);
        sb.Begin();

        float cx = _sw / 2f;
        float cy = _sh / 2f;

        // Title
        var titleSz = Assets.FontMedium.MeasureString("Myria");
        Gfx.Text(sb, Assets.FontMedium, "Myria",
            new Vector2(cx - titleSz.X / 2f, cy - 90), Theme.GoldSoft);

        if (_failed)
        {
            string msg = $"Failed to load:\n{_errorMessage}";
            var msgSz = Assets.FontSmall.MeasureString(msg);
            Gfx.Text(sb, Assets.FontSmall, msg,
                new Vector2(cx - msgSz.X / 2f, cy - 10), new Color(200, 80, 80));
            var escSz = Assets.FontSmall.MeasureString("ESC — return to menu");
            Gfx.Text(sb, Assets.FontSmall, "ESC — return to menu",
                new Vector2(cx - escSz.X / 2f, cy + 40), Theme.ForegroundDim);
        }
        else
        {
            // Orbiting-dot spinner
            const float radius = 22f;
            const int   dots   = 8;
            for (int i = 0; i < dots; i++)
            {
                float angle = _spinnerAngle + i * (MathF.Tau / dots);
                float alpha = (i + 1) / (float)dots;
                var   pos   = new Vector2(cx + MathF.Cos(angle) * radius,
                                          cy + MathF.Sin(angle) * radius);
                Gfx.Rect(sb, (int)pos.X - 2, (int)pos.Y - 2, 5, 5, Theme.Gold * alpha);
            }

            string label = _loadName != null
                ? $"Loading {_loadName}..."
                : "Loading world data...";
            var lSz = Assets.FontNormal.MeasureString(label);
            Gfx.Text(sb, Assets.FontNormal, label,
                new Vector2(cx - lSz.X / 2f, cy + 40), Theme.Foreground);
        }

        sb.End();
    }
}
