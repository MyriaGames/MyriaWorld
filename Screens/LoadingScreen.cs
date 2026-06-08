using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaLib.Entities.Players;
using MyriaWorld.Services;
using MyriaWorld.UI;

namespace MyriaWorld.Screens;

public class LoadingScreen : Screen
{
    private readonly Player _player;

    private volatile bool _done;
    private volatile bool _failed;
    private string _errorMessage = "";

    private float _spinnerAngle;
    private int _sw, _sh;

    public LoadingScreen(Player player) { _player = player; }

    // -----------------------------------------------------------------------

    public override void LoadContent()
    {
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        _sw = vp.Width;
        _sh = vp.Height;
    }

    public override void OnEnter()
    {
        if (WorldDataService.IsLoaded)
        {
            _done = true;
            return;
        }

        Task.Run(() =>
        {
            try
            {
                WorldDataService.Load();
                WorldDataService.PreparePlayer(_player);
                _done = true;
            }
            catch (Exception ex) { _errorMessage = ex.Message; _failed = true; }
        });
    }

    public override void Update(GameTime gt)
    {
        _spinnerAngle += (float)gt.ElapsedGameTime.TotalSeconds * 2.5f;

        if (_done)
        {
            // Replace this screen so ESC from the world goes back to main menu.
            ScreenManager.Instance.NavigateReplace(new WorldScreen(_player));
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
            var escSz = Assets.FontSmall.MeasureString("ESC - return to menu");
            Gfx.Text(sb, Assets.FontSmall, "ESC - return to menu",
                new Vector2(cx - escSz.X / 2f, cy + 40), Theme.ForegroundDim);
        }
        else
        {
            // Orbiting dot spinner
            const float radius = 22f;
            const int   dots   = 8;
            for (int i = 0; i < dots; i++)
            {
                float angle = _spinnerAngle + i * (MathF.Tau / dots);
                float alpha = (i + 1) / (float)dots;
                var   pos   = new Vector2(cx + MathF.Cos(angle) * radius,
                                          cy + MathF.Sin(angle) * radius);
                Gfx.Rect(sb, (int)pos.X - 2, (int)pos.Y - 2, 5, 5,
                    Theme.Gold * alpha);
            }

            const string label = "Loading world data...";
            var lSz = Assets.FontNormal.MeasureString(label);
            Gfx.Text(sb, Assets.FontNormal, label,
                new Vector2(cx - lSz.X / 2f, cy + 40), Theme.Foreground);
        }

        sb.End();
    }
}
