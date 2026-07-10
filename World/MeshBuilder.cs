using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyriaWorld.World;

/// <summary>
/// Shared geometry helpers used by both WorldScreen (player mesh) and
/// WorldMonster (monster meshes).  All methods work in local space; the
/// caller supplies a world matrix through BasicEffect.World before drawing.
/// </summary>
public static class MeshBuilder
{
    /// <summary>Appends a six-faced axis-aligned box to the supplied lists.</summary>
    public static void AddBox(
        List<VertexPositionColor> verts, List<int> idx,
        Vector3 min, Vector3 max, Color bright, Color dark)
    {
        Color mid = Lerp(bright, dark,       0.5f);
        Color top = Lerp(bright, Color.White, 0.25f);
        Color bot = Lerp(dark,   Color.Black, 0.4f);

        // Front (+Z), Back (-Z), Left (-X), Right (+X), Top (+Y), Bottom (-Y)
        AddQuad(verts, idx,
            new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), bright);
        AddQuad(verts, idx,
            new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), dark);
        AddQuad(verts, idx,
            new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), dark);
        AddQuad(verts, idx,
            new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), mid);
        AddQuad(verts, idx,
            new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z), top);
        AddQuad(verts, idx,
            new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), bot);
    }

    public static void AddQuad(
        List<VertexPositionColor> verts, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        int i = verts.Count;
        verts.Add(new VertexPositionColor(a, col));
        verts.Add(new VertexPositionColor(b, col));
        verts.Add(new VertexPositionColor(c, col));
        verts.Add(new VertexPositionColor(d, col));
        idx.AddRange([ i, i+1, i+2,   i, i+2, i+3 ]);
    }

    /// <summary>Appends a 4-sided pyramid (square base → apex) to the supplied lists.
    /// baseMin/baseMax are XZ corner positions at the base height (baseMin.Y == baseMax.Y).
    /// The base quad is omitted — only the four triangular sides are added.</summary>
    public static void AddPyramid(
        List<VertexPositionColor> verts, List<int> idx,
        Vector3 baseMin, Vector3 baseMax, float apexY,
        Color bright, Color dark)
    {
        float by = baseMin.Y;
        var sw   = new Vector3(baseMin.X, by, baseMin.Z);
        var se   = new Vector3(baseMax.X, by, baseMin.Z);
        var ne   = new Vector3(baseMax.X, by, baseMax.Z);
        var nw   = new Vector3(baseMin.X, by, baseMax.Z);
        var apex = new Vector3((baseMin.X + baseMax.X) * 0.5f, apexY,
                               (baseMin.Z + baseMax.Z) * 0.5f);

        Color mid = Lerp(bright, dark, 0.5f);
        AddTri(verts, idx, sw, se, apex, bright);   // front
        AddTri(verts, idx, se, ne, apex, mid);       // right
        AddTri(verts, idx, ne, nw, apex, dark);      // back
        AddTri(verts, idx, nw, sw, apex, mid);       // left
    }

    public static void AddTri(
        List<VertexPositionColor> verts, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        int i = verts.Count;
        verts.Add(new VertexPositionColor(a, col));
        verts.Add(new VertexPositionColor(b, col));
        verts.Add(new VertexPositionColor(c, col));
        idx.AddRange([ i, i+1, i+2 ]);
    }

    public static Color Lerp(Color a, Color b, float t) =>
        new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            255);
}
