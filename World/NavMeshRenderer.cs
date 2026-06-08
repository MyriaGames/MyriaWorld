using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyriaWorld.World;

/// <summary>
/// Converts a <see cref="NavMesh"/> into GPU geometry and draws it each frame.
/// Call <see cref="Build"/> once after the mesh is loaded, then <see cref="Draw"/>
/// every frame.  Dispose to release GPU buffers.
/// </summary>
public sealed class NavMeshRenderer : IDisposable
{
    // Face terrain → base colour
    private static readonly Dictionary<string, Color> TerrainColors = new()
    {
        ["grass"] = new Color(42,  68,  42),
        ["dirt"]  = new Color(90,  65,  40),
        ["stone"] = new Color(75,  75,  80),
        ["sand"]  = new Color(170, 145, 85),
        ["snow"]  = new Color(200, 210, 220),
        ["wood"]  = new Color(90,  65,  45),
        ["water"] = new Color(30,  60, 120),
    };

    // Use CullNone for the floor so winding order in the JSON doesn't matter.
    private static readonly RasterizerState NoCull = new() { CullMode = CullMode.None };

    private VertexBuffer? _vb;
    private IndexBuffer?  _ib;
    private int           _triCount;

    // Portal edge lines (CPU array — small enough to not need a VB)
    private VertexPositionColor[] _portalLines = [];

    public void Build(GraphicsDevice gd, NavMesh mesh)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();

        foreach (var face in mesh.Faces)
        {
            Color c = TerrainColors.GetValueOrDefault(face.Terrain, new Color(50, 50, 50));

            // Fan triangulation (correct for any convex polygon)
            int baseV = verts.Count;
            foreach (int vi in face.VertexIndices)
            {
                var xz = mesh.Vertices[vi];
                verts.Add(new VertexPositionColor(new Vector3(xz.X, 0f, xz.Y), c));
            }
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                idx.Add(baseV);
                idx.Add(baseV + i);
                idx.Add(baseV + i + 1);
            }
        }

        _triCount = idx.Count / 3;

        _vb = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
        _vb.SetData(verts.ToArray());

        _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Count, BufferUsage.WriteOnly);
        _ib.SetData(idx.ToArray());

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
                lines.Add(new VertexPositionColor(new Vector3(a.X, 0.05f, a.Y), portalCol));
                lines.Add(new VertexPositionColor(new Vector3(b.X, 0.05f, b.Y), portalCol));
            }
        }
        _portalLines = lines.ToArray();
    }

    public void Draw(GraphicsDevice gd, BasicEffect effect)
    {
        if (_vb == null || _ib == null) return;

        var prevRaster = gd.RasterizerState;
        gd.RasterizerState = NoCull;

        gd.SetVertexBuffer(_vb);
        gd.Indices = _ib;

        effect.World = Matrix.Identity;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triCount);
        }

        // Portal boundaries
        if (_portalLines.Length >= 2)
        {
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

    public void Dispose()
    {
        _vb?.Dispose();
        _ib?.Dispose();
    }
}
