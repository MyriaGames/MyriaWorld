using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaWorld.World;

namespace MyriaWorld.UI;

public sealed class FastTravelOverlay
{
    public bool IsOpen { get; private set; }

    private int                        _currentRoomId;
    private IReadOnlyList<WorldWaypoint> _discovered = [];
    private WorldWaypoint?             _pending;

    // Map rendering context — set when opened
    private Texture2D? _mapTex;
    private Rectangle  _mapArea;
    private WorldMapOverlay? _worldMap;

    // Per-frame hover state (set in Update, used in Draw)
    private WorldWaypoint? _hovered;

    // Per-frame icon screen positions for hit-testing (parallel to _discovered)
    private readonly List<(int sx, int sy)> _iconPositions = [];

    private const int IconR = 10; // click radius in pixels

    public WorldWaypoint? ConsumeTravel()
    {
        var t = _pending;
        _pending = null;
        return t;
    }

    public void Open(int currentRoomId, IReadOnlyList<WorldWaypoint> discovered,
                     WorldMapOverlay worldMap)
    {
        _currentRoomId = currentRoomId;
        _discovered    = discovered;
        _pending       = null;
        _hovered       = null;
        _mapTex        = worldMap.MapTexture;
        _mapArea       = worldMap.MapArea;
        _worldMap      = worldMap;
        IsOpen         = true;
    }

    public void Close() => IsOpen = false;

    // ── Update ───────────────────────────────────────────────────────────────

    public void Update(MouseState ms, MouseState prev)
    {
        if (!IsOpen || _worldMap == null) return;

        // Rebuild icon positions (also used by Draw)
        _iconPositions.Clear();
        foreach (var wp in _discovered)
        {
            var (sx, sy) = _worldMap.WorldToScreen(wp.Position.X, wp.Position.Z);
            _iconPositions.Add((sx, sy));
        }

        // Hover detection
        _hovered = null;
        for (int i = 0; i < _discovered.Count; i++)
        {
            var (sx, sy) = _iconPositions[i];
            int dx = ms.X - sx, dy = ms.Y - sy;
            if (dx * dx + dy * dy <= IconR * IconR)
            {
                _hovered = _discovered[i];
                break;
            }
        }

        // Click
        bool clicked = ms.LeftButton == ButtonState.Released
                    && prev.LeftButton == ButtonState.Pressed;
        if (clicked && _hovered != null && _hovered.RoomId != _currentRoomId)
        {
            _pending = _hovered;
            IsOpen   = false;
        }
    }

    // ── Draw ─────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, int sw, int sh)
    {
        if (!IsOpen || _mapTex == null) return;

        const int Pad  = 55;
        const int HdrH = 36;

        int panX = Pad, panY = Pad;
        int panW = sw - Pad * 2;
        int panH = sh - Pad * 2;

        // Dark backdrop
        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, 200));

        // Panel
        Gfx.Rect(sb, panX, panY, panW, panH, new Color(10, 8, 18, 245));
        Gfx.Rect(sb, panX, panY, panW, HdrH, new Color(20, 16, 40, 255));
        Gfx.Border(sb, new Rectangle(panX, panY, panW, panH), new Color(100, 160, 220, 200));

        // Header
        const string title = "Fast Travel";
        var tsz = Assets.FontNormal.MeasureString(title);
        Gfx.Text(sb, Assets.FontNormal, title,
            new Vector2(panX + (panW - tsz.X) / 2f, panY + (HdrH - tsz.Y) / 2f),
            new Color(140, 200, 255));

        const string hint = "Click waypoint to travel   ESC Close";
        var hsz = Assets.FontSmall.MeasureString(hint);
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2(panX + panW - hsz.X - 12, panY + (HdrH - hsz.Y) / 2f),
            new Color(120, 110, 140));

        // Map background
        sb.Draw(_mapTex, _mapArea, new Color(255, 255, 255, 220));

        // Waypoint icons
        for (int i = 0; i < _discovered.Count; i++)
        {
            var wp = _discovered[i];
            var (sx, sy) = _iconPositions.Count > i ? _iconPositions[i] : (-1, -1);
            if (sx < 0) continue;

            bool isCurr   = wp.RoomId == _currentRoomId;
            bool isHover  = wp == _hovered;

            // Outer ring
            Color ringCol = isCurr ? new Color(220, 180, 50) : new Color(80, 160, 255);
            Gfx.Rect(sb, sx - IconR, sy - IconR, IconR * 2, IconR * 2, ringCol);

            // Inner fill
            Color fillCol = isCurr ? new Color(255, 220, 80) : (isHover ? Color.White : new Color(140, 210, 255));
            Gfx.Rect(sb, sx - 5, sy - 5, 10, 10, fillCol);

            // Label
            Color labelCol = isCurr ? new Color(255, 220, 80)
                           : isHover ? Color.White
                           : new Color(210, 200, 175);
            string label = isCurr ? $"{wp.Name} (here)" : wp.Name;
            Gfx.Text(sb, Assets.FontSmall, label,
                new Vector2(sx + IconR + 4, sy - Assets.FontSmall.LineSpacing / 2f),
                labelCol);
        }
    }
}
