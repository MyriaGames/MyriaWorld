using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

/// <summary>
/// Bakes a worn-dirt path strip along every portal connection between two
/// open-terrain navmesh faces, so adjacent rooms read as connected by an
/// actual trail instead of just being two same-colored polygons that happen
/// to touch. Purely derived from navmesh connectivity (no per-room hardcoded
/// lists) so it stays correct as world.json grows — the same lesson learned
/// fixing WorldDecorations' stale hardcoded zone coordinates.
/// </summary>
public sealed class WorldPaths : IDisposable
{
    // Terrain types a path visually makes sense crossing — pavement/tunnel floors
    // (city, interior, cave, dungeon) already read as a walkable surface on their own.
    private static readonly HashSet<string> EligibleTerrain =
        new(StringComparer.OrdinalIgnoreCase) { "grass", "forest", "dirt", "stone", "sand", "snow" };

    private const float HalfWidth = 1.8f;
    private const float YOffset   = 0.03f; // avoid z-fighting with the floor mesh

    // A strip's winding order depends on which way A->B points in world space, so it
    // isn't guaranteed to face the camera the way hand-authored geometry is — cull
    // none rather than have roughly half of all strips back-face-culled invisible.
    private static readonly RasterizerState NoCull = new() { CullMode = CullMode.None };

    private VertexBuffer? _vb;
    private IndexBuffer?  _ib;
    private int           _triCount;

    public void Build(GraphicsDevice gd, NavMesh mesh, float[] heights)
    {
        var verts = new List<VertexPositionColorTexture>();
        var idx   = new List<int>();
        var seen  = new HashSet<(int, int)>();

        for (int fi = 0; fi < mesh.Faces.Length; fi++)
        {
            var face = mesh.Faces[fi];
            if (!EligibleTerrain.Contains(face.Terrain)) continue;

            foreach (var (neighbor, va, vb) in face.Portals)
            {
                if (neighbor <= fi) continue; // undirected — only process each portal once
                var key = (fi, neighbor);
                if (!seen.Add(key)) continue;

                var other = mesh.Faces[neighbor];
                if (!EligibleTerrain.Contains(other.Terrain)) continue;

                Vector2 a = FaceCentroid(mesh, fi);
                Vector2 b = FaceCentroid(mesh, neighbor);
                AddStrip(mesh, heights, fi, neighbor, a, b, verts, idx);
            }
        }

        if (verts.Count == 0) return;

        _triCount = idx.Count / 3;
        _vb = new VertexBuffer(gd, typeof(VertexPositionColorTexture), verts.Count, BufferUsage.WriteOnly);
        _vb.SetData(verts.ToArray());
        _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Count, BufferUsage.WriteOnly);
        _ib.SetData(idx.ToArray());
    }

    public void Draw(GraphicsDevice gd, BasicEffect effect)
    {
        if (_vb == null || _ib == null) return;

        var prevRaster = gd.RasterizerState;
        gd.RasterizerState = NoCull;

        effect.World   = Matrix.Identity;
        effect.Texture = ProceduralTextures.Path;
        gd.SetVertexBuffer(_vb);
        gd.Indices = _ib;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triCount);
        }

        gd.RasterizerState = prevRaster;
    }

    public void Dispose()
    {
        _vb?.Dispose();
        _ib?.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AddStrip(NavMesh mesh, float[] heights, int faceA, int faceB,
        Vector2 a, Vector2 b, List<VertexPositionColorTexture> verts, List<int> idx)
    {
        Vector2 dir = b - a;
        float   len = dir.Length();
        if (len < 0.01f) return;
        dir /= len;
        Vector2 perp = new(-dir.Y, dir.X);
        Vector2 offset = perp * HalfWidth;

        Vector2 al = a - offset, ar = a + offset;
        Vector2 bl = b - offset, br = b + offset;

        Vector3 p0 = ToWorld(mesh, heights, faceA, al);
        Vector3 p1 = ToWorld(mesh, heights, faceA, ar);
        Vector3 p2 = ToWorld(mesh, heights, faceB, br);
        Vector3 p3 = ToWorld(mesh, heights, faceB, bl);

        var col = Color.White;
        MeshBuilder.AddQuad(verts, idx, p0, p1, p2, p3, col);
    }

    private static Vector3 ToWorld(NavMesh mesh, float[] heights, int fallbackFace, Vector2 xz)
    {
        int fi = mesh.FindFaceIndex(xz);
        if (fi < 0) fi = fallbackFace;
        float y = TerrainHeights.GetHeight(heights, mesh, fi, xz.X, xz.Y) + YOffset;
        return new Vector3(xz.X, y, xz.Y);
    }

    private static Vector2 FaceCentroid(NavMesh mesh, int fi)
    {
        var face = mesh.Faces[fi];
        float sumX = 0f, sumZ = 0f;
        foreach (int vi in face.VertexIndices)
        {
            sumX += mesh.Vertices[vi].X;
            sumZ += mesh.Vertices[vi].Y;
        }
        int n = face.VertexIndices.Length;
        return new Vector2(sumX / n, sumZ / n);
    }
}
