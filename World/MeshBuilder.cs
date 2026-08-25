using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

/// <summary>
/// Shared geometry helpers used by both WorldScreen (player mesh) and
/// WorldMonster (monster meshes).  All methods work in local space; the
/// caller supplies a world matrix through BasicEffect.World before drawing.
/// UVs are derived from each face's own edge lengths (not a fixed [0,1] stretch),
/// so a texture tiles at a consistent world-space density regardless of a
/// quad/box's size — paired with a wrapping sampler state at draw time.
/// </summary>
public static class MeshBuilder
{
    /// <summary>World units per texture repeat.</summary>
    public const float TileSize = 1.25f;

    /// <summary>Appends a six-faced axis-aligned box to the supplied lists.</summary>
    public static void AddBox(
        List<VertexPositionColorTexture> verts, List<int> idx,
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

    /// <summary>Appends a planar quad a→b→c→d, with UVs tiled from its own edge lengths
    /// (a=(0,0), b=(|ab|/TileSize,0), d=(0,|ad|/TileSize)) so the texture repeats at a
    /// consistent world-space density no matter how large the quad is.</summary>
    public static void AddQuad(
        List<VertexPositionColorTexture> verts, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        float u = Vector3.Distance(a, b) / TileSize;
        float v = Vector3.Distance(a, d) / TileSize;

        int i = verts.Count;
        verts.Add(new VertexPositionColorTexture(a, col, new Vector2(0f, 0f)));
        verts.Add(new VertexPositionColorTexture(b, col, new Vector2(u,  0f)));
        verts.Add(new VertexPositionColorTexture(c, col, new Vector2(u,  v)));
        verts.Add(new VertexPositionColorTexture(d, col, new Vector2(0f, v)));
        idx.AddRange([ i, i+1, i+2,   i, i+2, i+3 ]);
    }

    /// <summary>Appends a 4-sided pyramid (square base → apex) to the supplied lists.
    /// baseMin/baseMax are XZ corner positions at the base height (baseMin.Y == baseMax.Y).
    /// The base quad is omitted — only the four triangular sides are added.</summary>
    public static void AddPyramid(
        List<VertexPositionColorTexture> verts, List<int> idx,
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

    /// <summary>Appends a triangle a→b→c with UVs tiled from a's local planar basis
    /// (a=(0,0), b=(|ab|/TileSize,0), c projected onto that basis) — same tiling
    /// convention as <see cref="AddQuad"/>.</summary>
    public static void AddTri(
        List<VertexPositionColorTexture> verts, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        Vector3 ab    = b - a;
        float   abLen = ab.Length();
        Vector3 abDir = abLen > 0.0001f ? ab / abLen : Vector3.UnitX;
        Vector3 ac    = c - a;
        float   cu    = Vector3.Dot(ac, abDir);
        float   cv    = (ac - abDir * cu).Length();

        int i = verts.Count;
        verts.Add(new VertexPositionColorTexture(a, col, Vector2.Zero));
        verts.Add(new VertexPositionColorTexture(b, col, new Vector2(abLen / TileSize, 0f)));
        verts.Add(new VertexPositionColorTexture(c, col, new Vector2(cu / TileSize, cv / TileSize)));
        idx.AddRange([ i, i+1, i+2 ]);
    }

    public static Color Lerp(Color a, Color b, float t) =>
        new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            255);
}
