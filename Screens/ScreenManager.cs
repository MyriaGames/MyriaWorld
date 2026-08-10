using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.Screens;

public class ScreenManager
{
    public static ScreenManager Instance { get; private set; } = null!;

    private Screen? _current;
    private Screen? _pending;
    private readonly Stack<Screen> _history = new();

    public GraphicsDevice GraphicsDevice { get; }
    public ContentManager Content        { get; }
    public GameWindow     Window         { get; }

    public ScreenManager(GraphicsDevice gd, ContentManager content, GameWindow window)
    {
        GraphicsDevice = gd;
        Content        = content;
        Window         = window;
        Instance       = this;
    }

    /// <summary>Navigate forward, pushing current screen onto the back stack.</summary>
    public void Navigate(Screen screen)
    {
        if (_current != null) _history.Push(_current);
        _pending = screen;
    }

    /// <summary>Replace current screen without adding it to the back stack.</summary>
    public void NavigateReplace(Screen screen)
    {
        _pending = screen;
    }

    public void GoBack()
    {
        if (_history.Count > 0) _pending = _history.Pop();
    }

    public void ClearHistory() => _history.Clear();

    public void Update(GameTime gameTime)
    {
        if (_pending != null)
        {
            _current?.OnExit();
            _current = _pending;
            _pending = null;
            if (!_current.IsLoaded)
            {
                _current.Initialize();
                _current.LoadContent();
                _current.IsLoaded = true;
            }
            _current.OnEnter();
        }
        _current?.Update(gameTime);
    }

    public void Draw(SpriteBatch sb)
    {
        _current?.Draw(sb);
    }

    public void OnTextInput(char c)
    {
        _current?.OnTextInput(c);
    }
}
