using Microsoft.Xna.Framework;

namespace Myria.Mono.World;

/// <summary>
/// The world navigation mesh.  Vertices are XZ positions (Y=0 plane).
/// A spatial grid accelerates point-in-face queries to O(1) average case.
/// </summary>
public class NavMesh
{
    public Vector2[] Vertices { get; }
    public NavFace[] Faces    { get; }

    private readonly Dictionary<(int, int), List<int>> _grid = new();
    private readonly float _cellSize;

    public NavMesh(Vector2[] vertices, NavFace[] faces, float cellSize = 60f)
    {
        Vertices  = vertices;
        Faces     = faces;
        _cellSize = cellSize;
        BuildGrid();
    }

    // -----------------------------------------------------------------------
    // Public queries
    // -----------------------------------------------------------------------

    /// <summary>Returns the index of the face that contains <paramref name="xz"/>,
    /// or -1 if no face covers that position.</summary>
    public int FindFaceIndex(Vector2 xz)
    {
        int cx = GridCell(xz.X), cz = GridCell(xz.Y);
        if (!_grid.TryGetValue((cx, cz), out var bucket)) return -1;
        foreach (int fi in bucket)
            if (PointInFace(xz, fi)) return fi;
        return -1;
    }

    public NavFace? FaceAt(Vector2 xz)
    {
        int fi = FindFaceIndex(xz);
        return fi >= 0 ? Faces[fi] : null;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private int GridCell(float v) => (int)MathF.Floor(v / _cellSize);

    private void BuildGrid()
    {
        for (int fi = 0; fi < Faces.Length; fi++)
        {
            var (minX, maxX, minZ, maxZ) = FaceBounds(fi);
            int x0 = GridCell(minX), x1 = GridCell(maxX);
            int z0 = GridCell(minZ), z1 = GridCell(maxZ);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    var key = (x, z);
                    if (!_grid.TryGetValue(key, out var list))
                        _grid[key] = list = new();
                    list.Add(fi);
                }
        }
    }

    private (float minX, float maxX, float minZ, float maxZ) FaceBounds(int fi)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (int vi in Faces[fi].VertexIndices)
        {
            var v = Vertices[vi];
            if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
            if (v.Y < minZ) minZ = v.Y;  if (v.Y > maxZ) maxZ = v.Y;
        }
        return (minX, maxX, minZ, maxZ);
    }

    /// <summary>Ray-casting point-in-polygon (works for any simple polygon).</summary>
    private bool PointInFace(Vector2 p, int fi)
    {
        var idx = Faces[fi].VertexIndices;
        bool inside = false;
        for (int i = 0, j = idx.Length - 1; i < idx.Length; j = i++)
        {
            var vi = Vertices[idx[i]];
            var vj = Vertices[idx[j]];
            if (((vi.Y > p.Y) != (vj.Y > p.Y)) &&
                p.X < (vj.X - vi.X) * (p.Y - vi.Y) / (vj.Y - vi.Y) + vi.X)
                inside = !inside;
        }
        return inside;
    }
}
