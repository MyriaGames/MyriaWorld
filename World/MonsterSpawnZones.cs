using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myria.Mono.Services;

namespace Myria.Mono.World;

/// <summary>
/// Marks out 1-2 explicit monster-spawn clearings per monster-bearing navmesh
/// face (deterministic per face, no hardcoded per-room data — same pattern as
/// WorldDecorationSpawner's random prop placement) and bakes a visible ground
/// decal for each so players can see where danger clusters instead of monsters
/// being able to spawn anywhere in the room, including on top of NPCs/buildings.
/// </summary>
public sealed class MonsterSpawnZones : IDisposable
{
    public readonly record struct Zone(Vector2 Center, float Radius);

    private const int   DecalSegments = 16;
    private const float MinRadius     = 4.0f;
    private const float MaxRadius     = 6.5f;
    private const float YOffset       = 0.02f;

    private static readonly RasterizerState NoCull = new() { CullMode = CullMode.None };

    private readonly Dictionary<int, List<Zone>> _byFace = new();

    private VertexBuffer? _vb;
    private IndexBuffer?  _ib;
    private int           _triCount;

    public void Build(GraphicsDevice gd, NavMesh mesh, float[] heights)
    {
        _byFace.Clear();

        var verts = new List<VertexPositionColorTexture>();
        var idx   = new List<int>();

        for (int fi = 0; fi < mesh.Faces.Length; fi++)
        {
            var face = mesh.Faces[fi];
            if (!face.RoomId.HasValue) continue;

            var room = WorldDataService.GetRoom(face.RoomId.Value);
            if (room == null || room.Monsters.Count == 0) continue;

            var zones = GenerateZones(mesh, fi);
            if (zones.Count == 0) continue;

            _byFace[fi] = zones;
            foreach (var z in zones)
                AddDecal(mesh, heights, fi, z, verts, idx);
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
        effect.Texture = ProceduralTextures.Clearing;
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

    /// <summary>Zones defined for a face, or empty if the face has none (caller should
    /// fall back to sampling the whole face).</summary>
    public IReadOnlyList<Zone> GetZones(int faceIndex) =>
        _byFace.TryGetValue(faceIndex, out var z) ? z : [];

    // ── Zone generation ──────────────────────────────────────────────────────

    private static List<Zone> GenerateZones(NavMesh mesh, int fi)
    {
        var face = mesh.Faces[fi];
        var rng  = new Random(fi * 92821 + 5); // deterministic per face

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (int vi in face.VertexIndices)
        {
            var v = mesh.Vertices[vi];
            if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
            if (v.Y < minZ) minZ = v.Y;  if (v.Y > maxZ) maxZ = v.Y;
        }

        int target = 1 + rng.Next(2); // 1-2 clearings
        var zones  = new List<Zone>();
        int tries  = 0;

        while (zones.Count < target && tries < target * 30)
        {
            tries++;
            float x = minX + (float)rng.NextDouble() * (maxX - minX);
            float z = minZ + (float)rng.NextDouble() * (maxZ - minZ);
            float radius = MinRadius + (float)rng.NextDouble() * (MaxRadius - MinRadius);
            var   center = new Vector2(x, z);

            if (mesh.FindFaceIndex(center) != fi) continue;
            if (zones.Any(existing =>
                    Vector2.DistanceSquared(existing.Center, center)
                    < MathF.Pow(existing.Radius + radius, 2) * 0.85f))
                continue;

            zones.Add(new Zone(center, radius));
        }

        if (zones.Count == 0)
        {
            // Fallback for tiny/oddly-shaped faces where rejection sampling never landed
            // inside the polygon — use the bounding-box center with a conservative radius.
            var center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float radius = MathF.Min(MaxRadius, MathF.Min(maxX - minX, maxZ - minZ) * 0.3f);
            if (radius > 1f) zones.Add(new Zone(center, radius));
        }

        return zones;
    }

    private static void AddDecal(NavMesh mesh, float[] heights, int fallbackFace, Zone zone,
        List<VertexPositionColorTexture> verts, List<int> idx)
    {
        int baseV = verts.Count;
        float tile = zone.Radius * 2f / MeshBuilder.TileSize;

        // Center vertex — UV must match the ring's center reference point (tile*0.5,
        // tile*0.5), not the origin, or the texture pinches oddly at the middle.
        verts.Add(MakeDecalVertex(mesh, heights, fallbackFace, zone.Center, new Vector2(tile * 0.5f)));

        for (int s = 0; s <= DecalSegments; s++)
        {
            float a = MathF.Tau * s / DecalSegments;
            var offset = new Vector2(MathF.Cos(a), MathF.Sin(a)) * zone.Radius;
            var pos    = zone.Center + offset;
            var uv     = new Vector2(MathF.Cos(a), MathF.Sin(a)) * (tile * 0.5f) + new Vector2(tile * 0.5f);
            verts.Add(MakeDecalVertex(mesh, heights, fallbackFace, pos, uv));
        }

        for (int s = 0; s < DecalSegments; s++)
        {
            idx.Add(baseV);
            idx.Add(baseV + s + 1);
            idx.Add(baseV + s + 2);
        }
    }

    private static VertexPositionColorTexture MakeDecalVertex(NavMesh mesh, float[] heights,
        int fallbackFace, Vector2 xz, Vector2 uv)
    {
        int fi = mesh.FindFaceIndex(xz);
        if (fi < 0) fi = fallbackFace;
        float y = TerrainHeights.GetHeight(heights, mesh, fi, xz.X, xz.Y) + YOffset;
        return new VertexPositionColorTexture(new Vector3(xz.X, y, xz.Y), Color.White, uv);
    }
}
