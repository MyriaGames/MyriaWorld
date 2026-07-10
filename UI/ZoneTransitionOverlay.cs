using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyriaWorld.UI;

/// <summary>
/// Brief fade-to-black overlay shown when the player crosses a major zone boundary
/// (e.g. entering or leaving a dungeon / cave). Plays once then returns to Idle.
/// </summary>
public sealed class ZoneTransitionOverlay
{
    private enum Phase { Idle, FadeIn, Hold, FadeOut }

    private Phase  _phase;
    private float  _timer;
    private string _zoneName = "";

    private const float FadeInTime  = 0.5f;
    private const float HoldTime    = 0.6f;
    private const float FadeOutTime = 0.5f;

    public bool IsActive => _phase != Phase.Idle;

    public void Trigger(string zoneName)
    {
        _zoneName = zoneName;
        _phase    = Phase.FadeIn;
        _timer    = 0f;
    }

    public void Update(float dt)
    {
        if (_phase == Phase.Idle) return;

        _timer += dt;
        switch (_phase)
        {
            case Phase.FadeIn  when _timer >= FadeInTime:
                _phase = Phase.Hold;  _timer = 0f; break;
            case Phase.Hold    when _timer >= HoldTime:
                _phase = Phase.FadeOut; _timer = 0f; break;
            case Phase.FadeOut when _timer >= FadeOutTime:
                _phase = Phase.Idle;  _timer = 0f; break;
        }
    }

    public void Draw(SpriteBatch sb, int sw, int sh)
    {
        if (_phase == Phase.Idle) return;

        float alpha = _phase switch
        {
            Phase.FadeIn  => _timer / FadeInTime,
            Phase.Hold    => 1f,
            Phase.FadeOut => 1f - _timer / FadeOutTime,
            _             => 0f,
        };

        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, (int)(240 * alpha)));

        if (_phase is Phase.Hold or Phase.FadeOut)
        {
            var  sz = Assets.FontMedium.MeasureString(_zoneName);
            Gfx.Text(sb, Assets.FontMedium, _zoneName,
                new Vector2((sw - sz.X) / 2f, (sh - sz.Y) / 2f),
                Theme.GoldSoft * Math.Min(alpha * 2f, 1f));
        }
    }
}
