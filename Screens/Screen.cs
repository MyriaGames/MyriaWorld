using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.Screens;

public abstract class Screen
{
    public bool IsLoaded { get; set; }

    public virtual void Initialize()   { }
    public abstract void LoadContent();
    public virtual void OnEnter()      { }
    public virtual void OnExit()       { }

    public abstract void Update(GameTime gameTime);
    public abstract void Draw(SpriteBatch sb);

    public virtual void OnTextInput(char c) { }
}
