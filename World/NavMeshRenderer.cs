using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

/// <summary>
/// Converts a <see cref="NavMesh"/> into GPU geometry and draws it each frame.
/// Call <see cref="Build"/> once after the mesh is loaded, then <see cref="Draw"/>
/// every frame.  Dispose to release GPU buffers.
/// </summary>
public sealed class NavMeshRenderer : IDisposable
{
    // Flat terrain tones used by the 2D minimap/world-map overlays (those stay
    // vertex-colored — no benefit from real texture sampling at that zoom level).
    private static readonly Dictionary<string, Color> TerrainColors = new()
    {
        ["grass"]    = new Color(42,  68,  42),
        ["forest"]   = new Color(28,  52,  28),
        ["dirt"]     = new Color(90,  65,  40),
        ["stone"]    = new Color(75,  75,  80),
        ["sand"]     = new Color(170, 145, 85),
        ["snow"]     = new Color(200, 210, 220),
        ["wood"]     = new Color(90,  65,  45),
        ["water"]    = new Color(30,  60, 120),
        ["cave"]     = new Color(35,  30,  40),
        ["dungeon"]  = new Color(25,  18,  30),
        ["city"]     = new Color(95,  88,  80),
        ["interior"] = new Color(145, 128, 100),
    };

    /// <summary>Returns the flat floor colour for the given terrain type string
    /// (used by the 2D minimap/world-map overlays).</summary>
    public static Color TerrainColor(string terrain)
        => TerrainColors.GetValueOrDefault(terrain, new Color(50, 50, 50));

    // Use CullNone for the floor so winding order in the JSON doesn't matter.
    private static readonly RasterizerState NoCull = new() { CullMode = CullMode.None };

    // One draw group per terrain type — each needs its own bound Texture2D, so they
    // can't share a single vertex/index buffer draw call the way the old flat-colored
    // floor did.
    private sealed class TerrainGroup
    {
        public required string       Terrain;
        public required VertexBuffer Vb;
        public required IndexBuffer  Ib;
        public required int          TriCount;
    }

    private readonly List<TerrainGroup> _groups = [];

    // Portal edge lines (CPU array — small enough to not need a VB)
    private VertexPositionColor[] _portalLines = [];

    public void Build(GraphicsDevice gd, NavMesh mesh, float[] heights)
    {
        DisposeGroups();

        // World-space UV so the sampler's wrap addressing tiles the terrain texture
        // at a consistent density regardless of face size.
        const float tile = 3.0f;

        var byTerrain = new Dictionary<string, (List<VertexPositionColorTexture> v, List<int> idx)>();

        foreach (var face in mesh.Faces)
        {
            if (!byTerrain.TryGetValue(face.Terrain, out var bucket))
                byTerrain[face.Terrain] = bucket = (new List<VertexPositionColorTexture>(), new List<int>());

            // Fan triangulation (correct for any convex polygon)
            int baseV = bucket.v.Count;
            foreach (int vi in face.VertexIndices)
            {
                var xz = mesh.Vertices[vi];
                float h = heights[vi];
                // Full-brightness vertex color with a subtle height-based shade so the
                // texture itself carries the terrain's hue instead of being darkened by
                // the old flat per-terrain fill color.
                float shade = MathHelper.Clamp(0.92f + h * 0.01f, 0.8f, 1.08f);
                var col = new Color(shade, shade, shade);
                bucket.v.Add(new VertexPositionColorTexture(
                    new Vector3(xz.X, h, xz.Y), col, new Vector2(xz.X / tile, xz.Y / tile)));
            }
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                bucket.idx.Add(baseV);
                bucket.idx.Add(baseV + i);
                bucket.idx.Add(baseV + i + 1);
            }
        }

        foreach (var (terrain, bucket) in byTerrain)
        {
            if (bucket.v.Count == 0) continue;

            var vb = new VertexBuffer(gd, typeof(VertexPositionColorTexture), bucket.v.Count, BufferUsage.WriteOnly);
            vb.SetData(bucket.v.ToArray());
            var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, bucket.idx.Count, BufferUsage.WriteOnly);
            ib.SetData(bucket.idx.ToArray());

            _groups.Add(new TerrainGroup { Terrain = terrain, Vb = vb, Ib = ib, TriCount = bucket.idx.Count / 3 });
        }

        // Portal edge lines — drawn 5 cm above floor to avoid Z-fighting
        Color portalCol = new Color(200, 170, 80, 160);
        var lines = new List<VertexPositionColor>();
        var seen  = new HashSet<(int, int)>();    // avoid drawing each portal twice

        foreach (var face in mesh.Faces)
        {
            foreach (var (_, va, vb) in face.Portals)
            {
                var key = (Math.Min(va, vb), Math.Max(va, vb));
                if (!seen.Add(key)) continue;
                var a = mesh.Vertices[va];
                var b = mesh.Vertices[vb];
                lines.Add(new VertexPositionColor(new Vector3(a.X, heights[va] + 0.05f, a.Y), portalCol));
                lines.Add(new VertexPositionColor(new Vector3(b.X, heights[vb] + 0.05f, b.Y), portalCol));
            }
        }
        _portalLines = lines.ToArray();
    }

    public void Draw(GraphicsDevice gd, BasicEffect effect)
    {
        if (_groups.Count == 0) return;

        var prevRaster = gd.RasterizerState;
        gd.RasterizerState = NoCull;

        effect.World = Matrix.Identity;
        effect.TextureEnabled = true;
        foreach (var group in _groups)
        {
            effect.Texture = ProceduralTextures.TerrainTile(group.Terrain);
            gd.SetVertexBuffer(group.Vb);
            gd.Indices = group.Ib;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, group.TriCount);
            }
        }

        // Portal boundaries — plain vertex-colored lines, no texture stage.
        if (_portalLines.Length >= 2)
        {
            effect.TextureEnabled = false;
            effect.World = Matrix.Identity;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.LineList,
                    _portalLines, 0, _portalLines.Length / 2);
            }
        }

        gd.RasterizerState = prevRaster;
    }

    private void DisposeGroups()
    {
        foreach (var g in _groups) { g.Vb.Dispose(); g.Ib.Dispose(); }
        _groups.Clear();
    }

    public void Dispose() => DisposeGroups();
}
