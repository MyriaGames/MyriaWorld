using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaWorld.World;

namespace MyriaWorld.UI;

/// <summary>
/// Full-screen world map opened with M.
/// Pre-renders the navmesh once to a RenderTarget2D, then each frame overlays
/// room labels for visited faces, the player dot, and NPC markers.
/// </summary>
public sealed class WorldMapOverlay : IDisposable
{
    // ── panel geometry ────────────────────────────────────────────────────────

    private const int Pad   = 55;    // screen margin around map panel
    private const int HdrH  = 36;   // header bar height

    private int _panX, _panY, _panW, _panH;  // panel (header + map area)
    private int _mapX, _mapY, _mapW, _mapH;  // map drawable area

    // ── world → map transform ─────────────────────────────────────────────────

    private float _cx, _cz;         // world-space centre of bounding box
    private float _spanX, _spanZ;   // world extents (with padding)

    // ── face centroids for label placement ───────────────────────────────────

    private Vector2[] _centroids = [];   // [faceIndex] world-space XZ centroid
    private string[]  _labels    = [];   // [faceIndex] display name

    // ── GPU resources ─────────────────────────────────────────────────────────

    private RenderTarget2D? _bg;

    // ── state ─────────────────────────────────────────────────────────────────

    public bool IsOpen { get; private set; }

    public void Toggle() => IsOpen = !IsOpen;
    public void Close()  => IsOpen = false;

    // ── build (called once in LoadContent) ───────────────────────────────────

    public void Build(GraphicsDevice gd, NavMesh mesh, BasicEffect effect, int sw, int sh)
    {
        _panX = Pad;         _panY = Pad;
        _panW = sw - Pad*2;  _panH = sh - Pad*2;
        _mapX = _panX;       _mapY = _panY + HdrH;
        _mapW = _panW;       _mapH = _panH - HdrH;

        // ── world bounding box ────────────────────────────────────────────────

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in mesh.Vertices)
        {
            if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
            if (v.Y < minZ) minZ = v.Y;  if (v.Y > maxZ) maxZ = v.Y;
        }
        _cx    = (minX + maxX) * 0.5f;
        _cz    = (minZ + maxZ) * 0.5f;
        _spanX = (maxX - minX) * 1.08f;
        _spanZ = (maxZ - minZ) * 1.08f;

        // ── face centroids & labels ───────────────────────────────────────────

        int n = mesh.Faces.Length;
        _centroids = new Vector2[n];
        _labels    = new string[n];
        for (int fi = 0; fi < n; fi++)
        {
            var face = mesh.Faces[fi];
            float sx = 0f, sz = 0f;
            foreach (int vi in face.VertexIndices)
            {
                sx += mesh.Vertices[vi].X;
                sz += mesh.Vertices[vi].Y;
            }
            int c = face.VertexIndices.Length;
            _centroids[fi] = new Vector2(sx / c, sz / c);
            _labels[fi]    = face.RoomName;
        }

        // ── pre-render navmesh to texture ─────────────────────────────────────

        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();
        foreach (var face in mesh.Faces)
        {
            Color col   = NavMeshRenderer.TerrainColor(face.Terrain);
            int   baseV = verts.Count;
            foreach (int vi in face.VertexIndices)
            {
                var xz = mesh.Vertices[vi];
                verts.Add(new VertexPositionColor(new Vector3(xz.X, 0f, xz.Y), col));
            }
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                idx.Add(baseV);
                idx.Add(baseV + i);
                idx.Add(baseV + i + 1);
            }
        }

        if (idx.Count == 0) return;

        var vb = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
        vb.SetData(verts.ToArray());
        var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Count, BufferUsage.WriteOnly);
        ib.SetData(idx.ToArray());

        var rt = new RenderTarget2D(gd, _mapW, _mapH, false,
                                    SurfaceFormat.Color, DepthFormat.None);

        // Save and override GPU state
        var savedDS    = gd.DepthStencilState;
        var savedBlend = gd.BlendState;
        var savedRas   = gd.RasterizerState;
        var savedView  = effect.View;
        var savedProj  = effect.Projection;
        var savedWorld = effect.World;

        gd.SetRenderTarget(rt);
        gd.Clear(new Color(12, 10, 18));

        gd.DepthStencilState = DepthStencilState.None;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullNone;

        // Non-square orthographic: world fits the map panel exactly
        effect.View  = Matrix.CreateLookAt(
            new Vector3(_cx, 100f, _cz),
            new Vector3(_cx,  0f,  _cz),
            new Vector3(0f,   0f, -1f));
        effect.Projection = Matrix.CreateOrthographic(_spanX, _spanZ, 0f, 200f);
        effect.World      = Matrix.Identity;

        gd.SetVertexBuffer(vb);
        gd.Indices = ib;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, idx.Count / 3);
        }

        gd.SetRenderTarget(null);

        // Restore
        gd.DepthStencilState = savedDS;
        gd.BlendState        = savedBlend;
        gd.RasterizerState   = savedRas;
        effect.View          = savedView;
        effect.Projection    = savedProj;
        effect.World         = savedWorld;

        vb.Dispose();
        ib.Dispose();
        _bg = rt;
    }

    // ── draw (inside SpriteBatch.Begin/End) ──────────────────────────────────

    public void Draw(SpriteBatch sb, int sw, int sh,
                     Vector3 playerPos, int currentFace,
                     HashSet<int> visited,
                     IEnumerable<WorldNpc> npcs)
    {
        if (!IsOpen || _bg == null) return;

        // ── dark backdrop ─────────────────────────────────────────────────────
        Gfx.Rect(sb, 0, 0, sw, sh, new Color(0, 0, 0, 210));

        // ── panel background & header ─────────────────────────────────────────
        Gfx.Rect(sb, _panX, _panY, _panW, _panH, new Color(10, 8, 18, 245));
        Gfx.Rect(sb, _panX, _panY, _panW, HdrH,  new Color(30, 24, 50, 255));
        Gfx.Border(sb, new Rectangle(_panX, _panY, _panW, _panH), new Color(160, 130, 55, 200));

        string title = "World Map";
        var    tsz   = Assets.FontNormal.MeasureString(title);
        Gfx.Text(sb, Assets.FontNormal, title,
            new Vector2(_panX + (_panW - tsz.X) / 2f, _panY + (HdrH - tsz.Y) / 2f),
            Theme.GoldSoft);

        string hint = "[M] Close";
        var    hsz  = Assets.FontSmall.MeasureString(hint);
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2(_panX + _panW - hsz.X - 10, _panY + (HdrH - hsz.Y) / 2f),
            new Color(150, 140, 110));

        // ── navmesh texture ───────────────────────────────────────────────────
        sb.Draw(_bg, new Rectangle(_mapX, _mapY, _mapW, _mapH), Color.White);

        // ── unvisited face darkening ──────────────────────────────────────────
        // Darken entire map slightly; visited faces punch through with labels only
        Gfx.Rect(sb, _mapX, _mapY, _mapW, _mapH, new Color(0, 0, 0, 100));

        // ── room labels for visited faces ─────────────────────────────────────
        for (int fi = 0; fi < _labels.Length; fi++)
        {
            if (!visited.Contains(fi) || string.IsNullOrEmpty(_labels[fi])) continue;
            var (px, py) = WorldToMap(_centroids[fi].X, _centroids[fi].Y);
            var sz = Assets.FontSmall.MeasureString(_labels[fi]);
            int lx = _mapX + px - (int)(sz.X / 2);
            int ly = _mapY + py - (int)(sz.Y / 2);

            bool isCurrent = fi == currentFace;
            Color labelCol = isCurrent ? new Color(255, 230, 100) : new Color(210, 200, 175);
            // Faint shadow for readability
            Gfx.Text(sb, Assets.FontSmall, _labels[fi], new Vector2(lx + 1, ly + 1),
                new Color(0, 0, 0, 140));
            Gfx.Text(sb, Assets.FontSmall, _labels[fi], new Vector2(lx, ly), labelCol);
        }

        // ── NPC markers ───────────────────────────────────────────────────────
        foreach (var npc in npcs)
        {
            var (nx, ny) = WorldToMap(npc.Position.X, npc.Position.Z);
            if (nx < 2 || nx > _mapW - 2 || ny < 2 || ny > _mapH - 2) continue;
            Gfx.Rect(sb, _mapX + nx - 3, _mapY + ny - 3, 7, 7, new Color(80, 200, 120, 200));
        }

        // ── player dot ────────────────────────────────────────────────────────
        var (ppx, ppy) = WorldToMap(playerPos.X, playerPos.Z);
        Gfx.Rect(sb, _mapX + ppx - 6, _mapY + ppy - 6, 13, 13, new Color(220, 180, 50, 220));
        Gfx.Rect(sb, _mapX + ppx - 3, _mapY + ppy - 3,  7,  7, Color.White);

        // ── legend ────────────────────────────────────────────────────────────
        DrawLegend(sb);
    }

    // ── legend ────────────────────────────────────────────────────────────────

    private static readonly (string label, Color col)[] LegendEntries =
    [
        ("City",    new Color( 95, 88, 80)),
        ("Grass",   new Color( 42, 68, 42)),
        ("Forest",  new Color( 28, 52, 28)),
        ("Stone",   new Color( 75, 75, 80)),
        ("Dirt",    new Color( 90, 65, 40)),
        ("Cave",    new Color( 35, 30, 40)),
        ("Dungeon", new Color( 25, 18, 30)),
    ];

    private void DrawLegend(SpriteBatch sb)
    {
        int x = _panX + 10;
        int y = _panY + _panH - 22;
        foreach (var (label, col) in LegendEntries)
        {
            Gfx.Rect(sb, x, y - 1, 12, 12, col);
            Gfx.Border(sb, new Rectangle(x, y - 1, 12, 12), new Color(120, 110, 90, 160));
            Gfx.Text(sb, Assets.FontSmall, label, new Vector2(x + 15, y - 3),
                new Color(180, 170, 145));
            x += (int)Assets.FontSmall.MeasureString(label).X + 30;
        }
    }

    // ── coordinate conversion ─────────────────────────────────────────────────

    private (int x, int y) WorldToMap(float wx, float wz)
    {
        int px = (int)(((wx - _cx) / _spanX + 0.5f) * _mapW);
        int py = (int)(((wz - _cz) / _spanZ + 0.5f) * _mapH);
        return (px, py);
    }

    /// <summary>Returns absolute screen pixel for a world-space XZ position.</summary>
    public (int sx, int sy) WorldToScreen(float wx, float wz)
    {
        var (px, py) = WorldToMap(wx, wz);
        return (_mapX + px, _mapY + py);
    }

    public Texture2D? MapTexture => _bg;
    public Rectangle  MapArea    => new(_mapX, _mapY, _mapW, _mapH);

    // ── cleanup ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _bg?.Dispose();
        _bg = null;
    }
}
