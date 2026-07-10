using Microsoft.Xna.Framework;

namespace MyriaWorld.World;

/// <summary>
/// Deterministic per-vertex terrain elevation used for visual hill rendering.
/// Each terrain type has a fixed base elevation (city=0, grass=4, stone=14, etc.)
/// plus smooth fractal noise scaled by an amplitude per terrain type.
/// The navmesh stays flat (Y=0) for collision/pathfinding; heights are visual only.
/// </summary>
public static class TerrainHeights
{
    public static float[] Build(NavMesh navMesh)
    {
        int n = navMesh.Vertices.Length;
        var baseSum = new float[n];
        var ampSum  = new float[n];
        var cnt     = new int[n];

        foreach (var face in navMesh.Faces)
        {
            float b = Base(face.Terrain);
            float a = Amplitude(face.Terrain);
            foreach (int vi in face.VertexIndices)
            {
                baseSum[vi] += b;
                ampSum[vi]  += a;
                cnt[vi]     += 1;
            }
        }

        var heights = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (cnt[i] == 0) continue;
            float b    = baseSum[i] / cnt[i];
            float a    = ampSum[i]  / cnt[i];
            var   vtx  = navMesh.Vertices[i];
            heights[i] = b + FractalNoise(vtx.X, vtx.Y) * a;
        }
        return heights;
    }

    /// <summary>
    /// Bilinear-interpolated height at world (x, z) within a navmesh face.
    /// Returns 0 for an invalid face index.
    /// </summary>
    public static float GetHeight(float[] heights, NavMesh mesh, int faceIndex, float x, float z)
    {
        if (faceIndex < 0 || faceIndex >= mesh.Faces.Length) return 0f;
        var face = mesh.Faces[faceIndex];

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (int vi in face.VertexIndices)
        {
            var vtx = mesh.Vertices[vi];
            if (vtx.X < minX) minX = vtx.X;
            if (vtx.X > maxX) maxX = vtx.X;
            if (vtx.Y < minZ) minZ = vtx.Y;
            if (vtx.Y > maxZ) maxZ = vtx.Y;
        }

        float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
        float hSW = 0f, hSE = 0f, hNE = 0f, hNW = 0f;
        foreach (int vi in face.VertexIndices)
        {
            var   vtx = mesh.Vertices[vi];
            float h   = heights[vi];
            bool  w   = vtx.X <= cx, s = vtx.Y <= cz;
            if      ( w &&  s) hSW = h;
            else if (!w &&  s) hSE = h;
            else if (!w && !s) hNE = h;
            else               hNW = h;
        }

        float dX = maxX - minX, dZ = maxZ - minZ;
        float tx  = dX > 0f ? Math.Clamp((x - minX) / dX, 0f, 1f) : 0f;
        float tz  = dZ > 0f ? Math.Clamp((z - minZ) / dZ, 0f, 1f) : 0f;

        return MathHelper.Lerp(
            MathHelper.Lerp(hSW, hSE, tx),
            MathHelper.Lerp(hNW, hNE, tx), tz);
    }

    // ── Terrain tiers ─────────────────────────────────────────────────────────
    // Base gives the semantic elevation (city = valley, stone = mountain peak).
    // Amplitude adds local noise variation on top of the base.

    private static float Base(string terrain) => terrain switch
    {
        "stone"   => 14f,
        "dirt"    =>  8f,
        "forest"  =>  6f,
        "grass"   =>  4f,
        "city"    =>  0f,
        "cave"    =>  0f,
        "dungeon" =>  0f,
        _         =>  2f
    };

    private static float Amplitude(string terrain) => terrain switch
    {
        "stone"   => 4.0f,
        "dirt"    => 3.0f,
        "forest"  => 2.5f,
        "grass"   => 1.5f,
        "city"    => 0.4f,
        "cave"    => 0.0f,
        "dungeon" => 0.0f,
        _         => 1.0f
    };

    // ── Noise ─────────────────────────────────────────────────────────────────

    private static float Hash(int ix, int iz)
    {
        float n = MathF.Sin(ix * 127.1f + iz * 311.7f) * 43758.5453f;
        return n - MathF.Floor(n);
    }

    private static float Smooth(float x, float z, float freq)
    {
        float sx = x / freq, sz = z / freq;
        int   ix = (int)MathF.Floor(sx), iz = (int)MathF.Floor(sz);
        float fx = sx - ix,  fz = sz - iz;
        float ux = fx * fx * (3 - 2 * fx);
        float uz = fz * fz * (3 - 2 * fz);
        return MathHelper.Lerp(
            MathHelper.Lerp(Hash(ix,     iz    ), Hash(ix + 1, iz    ), ux),
            MathHelper.Lerp(Hash(ix,     iz + 1), Hash(ix + 1, iz + 1), ux), uz);
    }

    private static float FractalNoise(float x, float z)
        => Smooth(x, z, 380f) * 0.60f
         + Smooth(x, z, 155f) * 0.28f
         + Smooth(x, z,  65f) * 0.12f;
}
