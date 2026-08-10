using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myria.Mono.Screens;
using Myria.Mono.Services;
using Myria.Mono.UI;

namespace Myria.Mono;

public class Game1 : Game
{
    public static Game1 Instance { get; private set; } = null!;

    /// <summary>Exposed so SettingsService can change resolution / fullscreen.</summary>
    public static GraphicsDeviceManager Display { get; private set; } = null!;

    private SpriteBatch _spriteBatch = null!;
    private ScreenManager _screenManager = null!;

    public Game1()
    {
        Instance = this;
        Display  = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth  = 1280,
            PreferredBackBufferHeight = 720,
        };
        Content.RootDirectory    = "Content";
        IsMouseVisible           = true;
        Window.Title             = "Myria";
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        // Apply saved display settings before the first frame
        SettingsService.Load();
        SettingsService.Apply();

        _screenManager = new ScreenManager(GraphicsDevice, Content, Window);
        Window.TextInput += (_, e) => _screenManager.OnTextInput(e.Character);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        Assets.Load(GraphicsDevice, Content);
        _screenManager.NavigateReplace(new MainMenuScreen());
    }

    protected override void Update(GameTime gameTime)
    {
        _screenManager.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _screenManager.Draw(_spriteBatch);
        base.Draw(gameTime);
    }
}
