using Microsoft.Xna.Framework;

namespace Myria.Mono.World;

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
    /// Height at world (x, z) within a navmesh face, via barycentric interpolation over
    /// the same fan-triangulation NavMeshRenderer uses to build the actual floor mesh
    /// (vertex 0, i, i+1 for i in [1, count-2]) — so this always matches what's rendered
    /// underfoot exactly, rather than approximating it.
    /// <para>
    /// A prior version bucketed the face's vertices into SW/SE/NE/NW corners of its
    /// axis-aligned bounding box and bilinearly interpolated those — a fine approximation
    /// for a roughly-rectangular face, but world.json is full of skewed quads and
    /// triangles where that bucketing diverges meaningfully from the real triangle the
    /// GPU rasterizes, which is exactly why props/NPCs/monsters would end up floating
    /// above or sunk into the visible floor.
    /// </para>
    /// Returns 0 for an invalid face index.
    /// </summary>
    public static float GetHeight(float[] heights, NavMesh mesh, int faceIndex, float x, float z)
    {
        if (faceIndex < 0 || faceIndex >= mesh.Faces.Length) return 0f;
        var idxs = mesh.Faces[faceIndex].VertexIndices;
        if (idxs.Length < 3) return 0f;

        var p = new Vector2(x, z);

        // Point normally falls inside exactly one fan triangle — use it directly.
        for (int i = 1; i < idxs.Length - 1; i++)
        {
            if (TryBarycentric(mesh, heights, p, idxs[0], idxs[i], idxs[i + 1], out float h, requireInside: true))
                return h;
        }

        // Fallback for a point just outside every triangle (float rounding at a shared
        // edge) — clamp barycentric weights into range per triangle and keep whichever
        // needed the least clamping, instead of silently returning a flat 0.
        float bestH = 0f, bestErr = float.MaxValue;
        for (int i = 1; i < idxs.Length - 1; i++)
        {
            if (TryBarycentric(mesh, heights, p, idxs[0], idxs[i], idxs[i + 1], out float h,
                    requireInside: false, error: out float err) && err < bestErr)
            {
                bestErr = err;
                bestH   = h;
            }
        }
        return bestH;
    }

    private static bool TryBarycentric(NavMesh mesh, float[] heights, Vector2 p,
        int ia, int ib, int ic, out float height, bool requireInside, out float error)
    {
        Vector2 a = mesh.Vertices[ia], b = mesh.Vertices[ib], c = mesh.Vertices[ic];
        Vector2 v0 = b - a, v1 = c - a, v2 = p - a;

        float d00 = Vector2.Dot(v0, v0), d01 = Vector2.Dot(v0, v1), d11 = Vector2.Dot(v1, v1);
        float d20 = Vector2.Dot(v2, v0), d21 = Vector2.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;

        if (MathF.Abs(denom) < 1e-6f) { height = heights[ia]; error = float.MaxValue; return false; }

        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1f - v - w;

        const float eps = 0.001f;
        bool inside = u >= -eps && v >= -eps && w >= -eps;

        if (requireInside)
        {
            error = 0f;
            if (!inside) { height = 0f; return false; }
            height = heights[ia] * u + heights[ib] * v + heights[ic] * w;
            return true;
        }

        // How far outside the triangle the raw weights were, before clamping — used to
        // pick the "closest" triangle when the point isn't cleanly inside any of them.
        error = MathF.Max(0f, -u) + MathF.Max(0f, -v) + MathF.Max(0f, -w);
        float cu = Math.Clamp(u, 0f, 1f), cv = Math.Clamp(v, 0f, 1f), cw = Math.Clamp(w, 0f, 1f);
        float sum = cu + cv + cw;
        if (sum > 1e-6f) { cu /= sum; cv /= sum; cw /= sum; }
        height = heights[ia] * cu + heights[ib] * cv + heights[ic] * cw;
        return true;
    }

    private static bool TryBarycentric(NavMesh mesh, float[] heights, Vector2 p,
        int ia, int ib, int ic, out float height, bool requireInside)
        => TryBarycentric(mesh, heights, p, ia, ib, ic, out height, requireInside, out _);

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
