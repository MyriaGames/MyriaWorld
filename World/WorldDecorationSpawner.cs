using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

/// <summary>
/// Generates all static world decorations (trees, houses, rocks, etc.) from the
/// navmesh terrain data and returns them as a single combined vertex/index buffer
/// ready for one draw call with Matrix.Identity world transform.
/// </summary>
public static class WorldDecorationSpawner
{
    private enum Deco { Tree, Pine, House, Rock, Grass, Stalactite }

    // Pre-planned building positions for Royas (face index, center X, center Z, name, npcId)
    private static readonly (int face, float cx, float cz, string name, string npcId)[] RoyasLayout =
    [
        (  0,  10f,  30f, "Healer's House",  "healer_default"    ),
        (  0, -18f,  20f, "Villager House",  "villager_ceralith"  ),
        ( 71, -25f, -52f, "Smithy",          "smith_default"      ),
        ( 71,  -5f, -52f, "Trading Post",    "trader_default"     ),
    ];

    // Waypoints: one shrine per city center (keyed by roomId)
    private static readonly (int roomId, string name)[] WaypointDefs =
    [
        ( 1, "Royas"),
        ( 9, "Hydea"),
        (27, "Gerlyas"),
        (61, "Xarra"),
    ];

    public static (VertexPositionColorTexture[] verts, int[] idx,
                   IReadOnlyList<WorldBuilding>  buildings,
                   IReadOnlyList<WorldWaypoint>  waypoints) Build(
        NavMesh navMesh, float[] heights)
    {
        var v   = new List<VertexPositionColorTexture>();
        var i   = new List<int>();
        var bl  = new List<WorldBuilding>();
        var wps = new List<WorldWaypoint>();

        for (int fi = 0; fi < navMesh.Faces.Length; fi++)
            SpawnFace(navMesh, fi, heights, v, i, bl, wps);

        return ([.. v], [.. i], bl, wps);
    }

    // ── Per-face spawning ─────────────────────────────────────────────────────

    private static void SpawnFace(NavMesh navMesh, int fi, float[] heights,
        List<VertexPositionColorTexture> v, List<int> i,
        List<WorldBuilding> buildings, List<WorldWaypoint> waypoints)
    {
        var face = navMesh.Faces[fi];

        // Royas NPC buildings: place at exact positions and register which NPC lives inside
        if (Array.FindIndex(RoyasLayout, e => e.face == fi) >= 0)
        {
            // Place a waypoint shrine at the center of face 0 (main Royas)
            PlaceWaypointIfNeeded(navMesh, fi, heights, v, i, waypoints);

            foreach (var e in RoyasLayout)
            {
                if (e.face != fi) continue;
                float y = TerrainHeights.GetHeight(heights, navMesh, fi, e.cx, e.cz);
                Append(Deco.House, new Vector3(e.cx, y, e.cz), 0f, null!, v, i);
                buildings.Add(new WorldBuilding(e.name, e.npcId, e.cx, e.cz));
            }
            return;
        }

        // Waypoint shrine on other city faces that host a waypoint definition
        PlaceWaypointIfNeeded(navMesh, fi, heights, v, i, waypoints);

        var rng  = new Random(fi * 7919 + 42);   // deterministic per face

        (int n, Deco[] palette) = face.Terrain switch
        {
            "forest"  => (7, new Deco[]{ Deco.Pine, Deco.Pine, Deco.Tree, Deco.Pine, Deco.Rock, Deco.Tree, Deco.Grass }),
            "grass"   => (5, new Deco[]{ Deco.Tree, Deco.Tree, Deco.Grass, Deco.Grass, Deco.Rock }),
            "city"    => (3, new Deco[]{ Deco.House, Deco.House, Deco.House }),
            "stone"   => (5, new Deco[]{ Deco.Rock, Deco.Rock, Deco.Rock, Deco.Rock, Deco.Rock }),
            "dirt"    => (4, new Deco[]{ Deco.Rock, Deco.Grass, Deco.Grass, Deco.Rock }),
            "cave"    => (4, new Deco[]{ Deco.Stalactite, Deco.Stalactite, Deco.Rock, Deco.Stalactite }),
            "dungeon" => (3, new Deco[]{ Deco.Stalactite, Deco.Rock, Deco.Stalactite }),
            _         => (0, new Deco[]{ })
        };

        if (n == 0) return;

        // Face bounding box (Vector2.Y maps to world Z)
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (int vi in face.VertexIndices)
        {
            var vtx = navMesh.Vertices[vi];
            if (vtx.X < minX) minX = vtx.X;
            if (vtx.X > maxX) maxX = vtx.X;
            if (vtx.Y < minZ) minZ = vtx.Y;
            if (vtx.Y > maxZ) maxZ = vtx.Y;
        }

        float pad = face.Terrain == "city" ? 12f : 7f;
        minX += pad; maxX -= pad;
        minZ += pad; maxZ -= pad;
        if (minX >= maxX || minZ >= maxZ) return;

        int placed = 0, tries = 0;
        while (placed < n && tries < n * 30)
        {
            tries++;
            float x = minX + (float)rng.NextDouble() * (maxX - minX);
            float z = minZ + (float)rng.NextDouble() * (maxZ - minZ);
            if (navMesh.FindFaceIndex(new Vector2(x, z)) != fi) continue;

            float rotY = (float)(rng.NextDouble() * MathF.Tau);
            float decoY = TerrainHeights.GetHeight(heights, navMesh, fi, x, z);
            Append(palette[placed % palette.Length],
                new Vector3(x, decoY, z), rotY, rng, v, i);
            placed++;
        }
    }

    // ── Waypoint shrine placement ─────────────────────────────────────────────

    // Shrine geometry lives in WorldWaypoint.MeshVerts — just register the waypoint here
    private static void PlaceWaypointIfNeeded(NavMesh navMesh, int fi, float[] heights,
        List<VertexPositionColorTexture> _, List<int> __, List<WorldWaypoint> waypoints)
    {
        var face = navMesh.Faces[fi];
        if (!face.RoomId.HasValue) return;

        var def = Array.Find(WaypointDefs, d => d.roomId == face.RoomId.Value);
        if (def == default) return;

        float cx = 0f, cz = 0f;
        foreach (int vi in face.VertexIndices)
        { cx += navMesh.Vertices[vi].X; cz += navMesh.Vertices[vi].Y; }
        cx /= face.VertexIndices.Length;
        cz /= face.VertexIndices.Length;

        float cy = TerrainHeights.GetHeight(heights, navMesh, fi, cx, cz);
        waypoints.Add(new WorldWaypoint(def.name, def.roomId, new Vector3(cx, cy, cz)));
    }

    // ── Append a single decoration ────────────────────────────────────────────

    private static void Append(Deco type, Vector3 pos, float rotY, Random rng,
        List<VertexPositionColorTexture> verts, List<int> idx)
    {
        var local  = new List<VertexPositionColorTexture>();
        var localI = new List<int>();

        switch (type)
        {
            case Deco.Tree:       BuildTree(local, localI);       break;
            case Deco.Pine:       BuildPine(local, localI);       break;
            case Deco.House:      BuildHouse(local, localI);      break;
            case Deco.Rock:       BuildRock(local, localI, rng);  break;
            case Deco.Grass:      BuildGrass(local, localI);      break;
            case Deco.Stalactite: BuildStalactite(local, localI); break;
        }

        var rot   = Matrix.CreateRotationY(rotY);
        int baseI = verts.Count;
        foreach (var vert in local)
            verts.Add(new VertexPositionColorTexture(
                Vector3.Transform(vert.Position, rot) + pos, vert.Color, vert.TextureCoordinate));
        foreach (int i in localI)
            idx.Add(baseI + i);
    }

    // ── Geometry builders ─────────────────────────────────────────────────────

    private static void BuildTree(List<VertexPositionColorTexture> v, List<int> i)
    {
        var trunkB = new Color(100, 65, 30);
        var trunkD = new Color(65,  40, 18);
        var leafB  = new Color(58, 128, 38);
        var leafD  = new Color(38,  88, 22);

        MeshBuilder.AddBox(v, i,
            new Vector3(-0.22f, 0f,  -0.22f),
            new Vector3( 0.22f, 1.2f, 0.22f),
            trunkB, trunkD);

        MeshBuilder.AddPyramid(v, i,
            new Vector3(-1.2f, 0.85f, -1.2f),
            new Vector3( 1.2f, 0.85f,  1.2f),
            3.0f, leafB, leafD);
    }

    private static void BuildPine(List<VertexPositionColorTexture> v, List<int> i)
    {
        var trunkB = new Color(85, 52, 22);
        var trunkD = new Color(58, 35, 14);
        var g1     = new Color(32,  98, 32);
        var g2     = new Color(22,  70, 22);
        var g3     = new Color(28,  84, 28);

        MeshBuilder.AddBox(v, i,
            new Vector3(-0.15f, 0f,  -0.15f),
            new Vector3( 0.15f, 1.4f, 0.15f),
            trunkB, trunkD);

        MeshBuilder.AddPyramid(v, i,
            new Vector3(-1.35f, 0.4f, -1.35f),
            new Vector3( 1.35f, 0.4f,  1.35f),
            1.55f, g1, g2);

        MeshBuilder.AddPyramid(v, i,
            new Vector3(-0.9f, 0.95f, -0.9f),
            new Vector3( 0.9f, 0.95f,  0.9f),
            2.2f, g2, g3);

        MeshBuilder.AddPyramid(v, i,
            new Vector3(-0.52f, 1.55f, -0.52f),
            new Vector3( 0.52f, 1.55f,  0.52f),
            2.8f, g1, g3);
    }

    private static void BuildHouse(List<VertexPositionColorTexture> v, List<int> i)
    {
        const float w = 6f, h = 3.8f, d = 6f;
        var wallB = new Color(198, 178, 148);
        var wallD = new Color(158, 138, 110);
        var roofB = new Color(148,  68,  38);
        var roofD = new Color(108,  48,  26);

        MeshBuilder.AddBox(v, i,
            new Vector3(-w * 0.5f, 0f,  -d * 0.5f),
            new Vector3( w * 0.5f, h,    d * 0.5f),
            wallB, wallD);

        MeshBuilder.AddPyramid(v, i,
            new Vector3(-w * 0.5f - 0.4f, h, -d * 0.5f - 0.4f),
            new Vector3( w * 0.5f + 0.4f, h,  d * 0.5f + 0.4f),
            h + 2.4f, roofB, roofD);
    }

    private static void BuildRock(List<VertexPositionColorTexture> v, List<int> i, Random rng)
    {
        float sx = 0.6f + (float)rng.NextDouble() * 0.7f;
        float sy = 0.35f + (float)rng.NextDouble() * 0.4f;
        float sz = 0.5f + (float)rng.NextDouble() * 0.6f;
        var r1 = new Color(115, 112, 105);
        var r2 = new Color(78,  76,  70);

        MeshBuilder.AddBox(v, i,
            new Vector3(-sx * 0.5f, 0f,  -sz * 0.5f),
            new Vector3( sx * 0.5f, sy,   sz * 0.5f),
            r1, r2);
    }

    private static void BuildGrass(List<VertexPositionColorTexture> v, List<int> i)
    {
        var g1 = new Color(82, 152, 42);
        var g2 = new Color(56, 108, 28);
        const float h = 0.6f, hw = 0.45f;

        // Two crossing quads (each added both ways for double-sided visibility)
        MeshBuilder.AddQuad(v, i,
            new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f,  0f),
            new Vector3(hw * 0.5f, h, 0f), new Vector3(-hw * 0.5f, h, 0f), g1);
        MeshBuilder.AddQuad(v, i,
            new Vector3(hw, 0f, 0f), new Vector3(-hw, 0f,  0f),
            new Vector3(-hw * 0.5f, h, 0f), new Vector3(hw * 0.5f, h, 0f), g1);

        MeshBuilder.AddQuad(v, i,
            new Vector3(0f, 0f, -hw), new Vector3(0f, 0f,  hw),
            new Vector3(0f, h, hw * 0.5f), new Vector3(0f, h, -hw * 0.5f), g2);
        MeshBuilder.AddQuad(v, i,
            new Vector3(0f, 0f, hw), new Vector3(0f, 0f, -hw),
            new Vector3(0f, h, -hw * 0.5f), new Vector3(0f, h, hw * 0.5f), g2);
    }

    private static void BuildStalactite(List<VertexPositionColorTexture> v, List<int> i)
    {
        var s1 = new Color(125, 120, 110);
        var s2 = new Color(85,  82,  75);

        MeshBuilder.AddPyramid(v, i,
            new Vector3(-0.22f, 0f, -0.22f),
            new Vector3( 0.22f, 0f,  0.22f),
            1.6f, s1, s2);
    }
}
