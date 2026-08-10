using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

/// <summary>
/// Pre-baked static prop geometry (trees, rocks, pillars, walls) for each zone.
/// Build once in LoadContent; call Draw each frame after the navmesh floor.
/// </summary>
public sealed class WorldDecorations : IDisposable
{
    private VertexBuffer? _vb;
    private IndexBuffer?  _ib;
    private int           _triCount;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Build(GraphicsDevice gd)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();

        BuildLumina(verts, idx);
        BuildForestEdge(verts, idx);
        BuildSouthFields(verts, idx);
        BuildEastGate(verts, idx);
        BuildWestMeadow(verts, idx);
        BuildCaveEntrance(verts, idx);
        BuildNuvmitoTrail(verts, idx);
        BuildPlateauPass(verts, idx);
        BuildEchoChamber(verts, idx);
        BuildNuvmitoTurn(verts, idx);
        BuildPlateauSouth2(verts, idx);
        BuildWhisperingWoods(verts, idx);

        if (verts.Count == 0) return;

        _triCount = idx.Count / 3;
        _vb = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
        _vb.SetData(verts.ToArray());
        _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Count, BufferUsage.WriteOnly);
        _ib.SetData(idx.ToArray());
    }

    public void Draw(GraphicsDevice gd, BasicEffect effect)
    {
        if (_vb == null || _ib == null) return;
        gd.SetVertexBuffer(_vb);
        gd.Indices = _ib;
        effect.World = Matrix.Identity;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triCount);
        }
    }

    public void Dispose()
    {
        _vb?.Dispose();
        _ib?.Dispose();
    }

    // ── Zone builders ─────────────────────────────────────────────────────────

    // Zone 0 – Lumina (grass plaza, X[-60,60] Z[-60,60])
    private static void BuildLumina(List<VertexPositionColor> v, List<int> i)
    {
        // Perimeter trees — pull in 8 u from each edge so they're inside the face
        float[] northSouthX = { -44, -28, -12, 12, 28, 44 };
        foreach (float x in northSouthX)
        {
            AddTree(v, i, new Vector3(x, 0f, -51f));
            AddTree(v, i, new Vector3(x, 0f,  51f));
        }
        float[] eastWestZ = { -42, -22, 0f, 22, 42 };
        foreach (float z in eastWestZ)
        {
            AddTree(v, i, new Vector3(-51f, 0f, z));
            AddTree(v, i, new Vector3( 51f, 0f, z));
        }

        // Central stone obelisk / waypoint marker
        MeshBuilder.AddBox(v, i,
            new Vector3(-0.6f, 0f, -0.6f), new Vector3(0.6f, 3.8f, 0.6f),
            new Color(120, 110, 100), new Color(80, 72, 65));
        // Cap
        MeshBuilder.AddBox(v, i,
            new Vector3(-0.5f, 3.8f, -0.5f), new Vector3(0.5f, 4.6f, 0.5f),
            new Color(190, 160, 60), new Color(140, 110, 40));

        // Benches/low walls around the obelisk
        AddBench(v, i, new Vector3(-5f, 0f,  0f),  0f);
        AddBench(v, i, new Vector3( 5f, 0f,  0f), MathF.PI);
        AddBench(v, i, new Vector3( 0f, 0f, -5f), MathF.PI / 2f);
        AddBench(v, i, new Vector3( 0f, 0f,  5f), -MathF.PI / 2f);

        // Scattered rocks near corners
        AddRock(v, i, new Vector3(-40f, 0f, -38f), 0.7f);
        AddRock(v, i, new Vector3( 38f, 0f,  36f), 0.8f);
        AddRock(v, i, new Vector3(-35f, 0f,  40f), 0.6f);
        AddRock(v, i, new Vector3( 40f, 0f, -40f), 0.9f);
    }

    // Zone 1 – Forest Edge (grass, X[-60,60] Z[-180,-60])
    private static void BuildForestEdge(List<VertexPositionColor> v, List<int> i)
    {
        // Dense irregular grid of trees — leave a loose corridor near x=0 for travel
        (float x, float z, float h)[] trees =
        {
            (-50f, -75f, 3.2f), (-34f, -80f, 2.8f), (-18f, -85f, 3.0f),
            ( 20f, -78f, 2.6f), ( 36f, -82f, 3.1f), ( 50f, -74f, 2.9f),
            (-52f, -98f, 3.5f), (-38f,-100f, 2.7f), (-22f,-108f, 3.3f),
            ( 18f, -95f, 2.9f), ( 38f, -99f, 3.4f), ( 52f, -92f, 2.8f),
            (-48f,-118f, 3.0f), (-30f,-122f, 3.2f), (-12f,-130f, 2.6f),
            ( 14f,-115f, 3.1f), ( 32f,-120f, 2.9f), ( 50f,-112f, 3.3f),
            (-54f,-140f, 3.4f), (-36f,-148f, 2.7f), ( -8f,-142f, 3.0f),
            ( 16f,-135f, 3.2f), ( 42f,-145f, 2.8f), ( 54f,-136f, 3.1f),
            (-46f,-162f, 3.0f), (-26f,-168f, 2.9f), (  2f,-170f, 3.3f),
            ( 22f,-160f, 2.7f), ( 46f,-165f, 3.1f),
        };
        foreach (var (x, z, h) in trees)
            AddTree(v, i, new Vector3(x, 0f, z), h);

        // Log on ground and rocks
        AddRock(v, i, new Vector3(-28f, 0f, -95f),  1.2f);
        AddRock(v, i, new Vector3( 24f, 0f,-138f),  1.0f);
        AddRock(v, i, new Vector3(-10f, 0f,-162f),  1.3f);
        AddStump(v, i, new Vector3(-15f, 0f, -90f));
        AddStump(v, i, new Vector3( 28f, 0f,-160f));
    }

    // Zone 2 – South Fields (dirt, X[-60,60] Z[60,180])
    private static void BuildSouthFields(List<VertexPositionColor> v, List<int> i)
    {
        // Rocks strewn across the dirt field
        AddRock(v, i, new Vector3(-38f, 0f,  80f), 1.5f);
        AddRock(v, i, new Vector3( 30f, 0f,  90f), 1.1f);
        AddRock(v, i, new Vector3(-18f, 0f, 108f), 1.3f);
        AddRock(v, i, new Vector3( 44f, 0f, 115f), 1.0f);
        AddRock(v, i, new Vector3(-46f, 0f, 132f), 1.4f);
        AddRock(v, i, new Vector3( 14f, 0f, 148f), 0.9f);
        AddRock(v, i, new Vector3(-30f, 0f, 165f), 1.2f);
        AddRock(v, i, new Vector3( 48f, 0f, 170f), 1.1f);

        // Stumps
        AddStump(v, i, new Vector3( 12f, 0f,  76f));
        AddStump(v, i, new Vector3(-16f, 0f, 128f));
        AddStump(v, i, new Vector3( 34f, 0f, 162f));

        // Tree line along west and east edges
        float[] treeZ = { 82f, 105f, 128f, 155f, 172f };
        foreach (float z in treeZ)
        {
            AddTree(v, i, new Vector3(-52f, 0f, z), 2.5f);
            AddTree(v, i, new Vector3( 52f, 0f, z), 2.3f);
        }

        // Low dirt mound / ruined wall fragment (south edge)
        MeshBuilder.AddBox(v, i,
            new Vector3(-35f, 0f, 172f), new Vector3(35f, 1.2f, 178f),
            new Color(105, 78, 48), new Color(72, 52, 30));
    }

    // Zone 3 – East Gate (stone, X[60,180] Z[-60,60])
    private static void BuildEastGate(List<VertexPositionColor> v, List<int> i)
    {
        // Entrance gate — two tall pillars with cross-beam
        AddPillar(v, i, new Vector3(72f, 0f, -22f), 5.2f);
        AddPillar(v, i, new Vector3(72f, 0f,  22f), 5.2f);
        MeshBuilder.AddBox(v, i,
            new Vector3(70f, 4.9f, -24f), new Vector3(74f, 5.7f, 24f),
            new Color(88, 84, 80), new Color(60, 57, 54));

        // Inner pillars forming a colonnade
        float[] colZ  = { -42f, -14f, 14f, 42f };
        float[] colX  = { 108f, 148f };
        foreach (float x in colX)
            foreach (float z in colZ)
                AddPillar(v, i, new Vector3(x, 0f, z), 4.0f);

        // North and south perimeter walls
        MeshBuilder.AddBox(v, i,
            new Vector3(65f, 0f, -57f), new Vector3(178f, 2.0f, -52f),
            new Color(88, 84, 80), new Color(62, 59, 56));
        MeshBuilder.AddBox(v, i,
            new Vector3(65f, 0f, 52f), new Vector3(178f, 2.0f, 57f),
            new Color(88, 84, 80), new Color(62, 59, 56));

        // Rubble / rocks
        AddRock(v, i, new Vector3(118f, 0f, -30f), 1.2f);
        AddRock(v, i, new Vector3(160f, 0f,  28f), 0.9f);
        AddRock(v, i, new Vector3( 90f, 0f,  48f), 1.1f);

        // End-wall with a gate arch shape (back of zone)
        MeshBuilder.AddBox(v, i,
            new Vector3(170f, 0f, -55f), new Vector3(178f, 4.5f, -22f),
            new Color(80, 77, 73), new Color(55, 52, 49));
        MeshBuilder.AddBox(v, i,
            new Vector3(170f, 0f,  22f), new Vector3(178f, 4.5f,  55f),
            new Color(80, 77, 73), new Color(55, 52, 49));
        MeshBuilder.AddBox(v, i,
            new Vector3(170f, 4.5f, -55f), new Vector3(178f, 5.5f, 55f),
            new Color(75, 72, 68), new Color(52, 50, 47));
    }

    // Zone 4 – West Meadow (grass, X[-180,-60] Z[-60,60])
    private static void BuildWestMeadow(List<VertexPositionColor> v, List<int> i)
    {
        // Scattered trees across the meadow
        (float x, float z, float h)[] trees =
        {
            ( -78f, -44f, 2.8f), ( -78f,  44f, 2.6f),
            ( -96f, -30f, 3.0f), ( -96f,  30f, 2.9f), ( -96f,   0f, 3.2f),
            (-116f, -48f, 2.7f), (-116f,  48f, 2.9f),
            (-134f, -18f, 3.1f), (-134f,  20f, 2.8f), (-134f,   0f, 3.3f),
            (-154f, -40f, 2.9f), (-154f,  38f, 3.0f),
            (-168f,  -8f, 3.2f), (-168f,  10f, 2.7f),
        };
        foreach (var (x, z, h) in trees)
            AddTree(v, i, new Vector3(x, 0f, z), h);

        // Rocks
        AddRock(v, i, new Vector3( -88f, 0f, -28f), 1.1f);
        AddRock(v, i, new Vector3(-148f, 0f,  34f), 1.3f);
        AddRock(v, i, new Vector3(-112f, 0f,   8f), 0.8f);

        // Old well / stone structure at the meadow center
        AddWell(v, i, new Vector3(-120f, 0f, 0f));
    }

    // ── Prop helpers ──────────────────────────────────────────────────────────

    private static void AddTree(List<VertexPositionColor> v, List<int> i,
                                Vector3 pos, float trunkH = 2.4f)
    {
        // Trunk
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.22f, 0f,    -0.22f),
            pos + new Vector3( 0.22f, trunkH, 0.22f),
            new Color(90, 55, 25), new Color(62, 38, 14));

        // Lower canopy
        float cy = trunkH * 0.55f;
        MeshBuilder.AddBox(v, i,
            new Vector3(pos.X - 1.35f, pos.Y + cy,        pos.Z - 1.35f),
            new Vector3(pos.X + 1.35f, pos.Y + cy + 1.7f, pos.Z + 1.35f),
            new Color(36, 76, 30), new Color(24, 52, 18));

        // Upper canopy
        MeshBuilder.AddBox(v, i,
            new Vector3(pos.X - 0.85f, pos.Y + cy + 1.4f, pos.Z - 0.85f),
            new Vector3(pos.X + 0.85f, pos.Y + cy + 2.7f, pos.Z + 0.85f),
            new Color(44, 88, 36), new Color(30, 60, 22));
    }

    private static void AddRock(List<VertexPositionColor> v, List<int> i,
                                Vector3 pos, float scale = 1f)
    {
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.62f * scale, 0f,            -0.50f * scale),
            pos + new Vector3( 0.62f * scale,  0.68f * scale,  0.50f * scale),
            new Color(102, 97, 92), new Color(68, 64, 60));
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.36f * scale,  0.65f * scale, -0.32f * scale),
            pos + new Vector3( 0.36f * scale,  0.98f * scale,  0.32f * scale),
            new Color(112, 106, 100), new Color(76, 72, 68));
    }

    private static void AddPillar(List<VertexPositionColor> v, List<int> i,
                                  Vector3 pos, float h = 4.5f)
    {
        // Shaft
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.38f, 0f, -0.38f),
            pos + new Vector3( 0.38f, h,   0.38f),
            new Color(96, 92, 88), new Color(65, 62, 58));
        // Capital
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.54f, h,       -0.54f),
            pos + new Vector3( 0.54f, h + 0.3f,  0.54f),
            new Color(108, 103, 98), new Color(74, 70, 66));
    }

    private static void AddStump(List<VertexPositionColor> v, List<int> i, Vector3 pos)
    {
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.42f, 0f,    -0.42f),
            pos + new Vector3( 0.42f, 0.48f,  0.42f),
            new Color(94, 64, 34), new Color(64, 42, 20));
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.48f, 0.46f, -0.48f),
            pos + new Vector3( 0.48f, 0.58f,  0.48f),
            new Color(78, 54, 26), new Color(52, 36, 16));
    }

    // Zone 8 – Echo Chamber (stone, X[-60,60] Z[-420,-300])
    private static void BuildEchoChamber(List<VertexPositionColor> v, List<int> i)
    {
        // Glowing mushroom clusters
        AddMushroom(v, i, new Vector3(-38f, 0f, -318f), 1.4f);
        AddMushroom(v, i, new Vector3(-32f, 0f, -322f), 0.8f);
        AddMushroom(v, i, new Vector3( 42f, 0f, -345f), 1.2f);
        AddMushroom(v, i, new Vector3( 38f, 0f, -350f), 0.7f);
        AddMushroom(v, i, new Vector3(-10f, 0f, -380f), 1.5f);
        AddMushroom(v, i, new Vector3( -5f, 0f, -388f), 0.9f);
        AddMushroom(v, i, new Vector3( 22f, 0f, -408f), 1.1f);

        // Heavy boulder formations
        AddRock(v, i, new Vector3(-44f, 0f, -312f), 2.0f);
        AddRock(v, i, new Vector3( 40f, 0f, -330f), 1.8f);
        AddRock(v, i, new Vector3(-20f, 0f, -365f), 1.6f);
        AddRock(v, i, new Vector3( 32f, 0f, -395f), 1.4f);
        AddRock(v, i, new Vector3(-48f, 0f, -410f), 2.2f);
        AddRock(v, i, new Vector3( 10f, 0f, -415f), 1.0f);

        // Ore seam veins running along cave walls (metallic highlight)
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f,   -340f), new Vector3(-50f, 2.8f, -310f),
            new Color(88, 82, 75), new Color(58, 54, 48));
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0.6f, -340f), new Vector3(-50f, 1.2f, -310f),
            new Color(175, 158, 82), new Color(118, 106, 52));  // gold streak
        MeshBuilder.AddBox(v, i,
            new Vector3( 52f, 0f,   -380f), new Vector3( 60f, 3.2f, -350f),
            new Color(88, 82, 75), new Color(58, 54, 48));
        MeshBuilder.AddBox(v, i,
            new Vector3( 52f, 0.8f, -380f), new Vector3( 60f, 1.4f, -350f),
            new Color(162, 148, 78), new Color(108, 98, 50));

        // Collapsed mine supports — broken frames on the floor
        AddTimber(v, i, new Vector3(-15f, 0f, -335f), 2.4f);
        AddTimber(v, i, new Vector3( 12f, 0f, -395f), 2.2f);

        // Far north rockfall blocking passage (suggests deeper dungeon beyond)
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, -418f), new Vector3( -8f, 4.5f, -412f),
            new Color(80, 76, 72), new Color(54, 51, 48));
        MeshBuilder.AddBox(v, i,
            new Vector3( 15f, 0f, -418f), new Vector3( 60f, 3.8f, -412f),
            new Color(80, 76, 72), new Color(54, 51, 48));
        AddRock(v, i, new Vector3(  0f, 0f, -413f), 2.4f);
    }

    // Zone 9 – Nuvmito Turn (grass, X[300,420] Z[-60,60])
    private static void BuildNuvmitoTurn(List<VertexPositionColor> v, List<int> i)
    {
        // Dramatic viewpoint rock formation at the bend (northeast corner)
        AddRock(v, i, new Vector3(395f, 0f, -46f), 2.8f);
        AddRock(v, i, new Vector3(408f, 0f, -38f), 2.2f);
        AddRock(v, i, new Vector3(400f, 0f, -20f), 1.6f);

        // Scree and boulders along the trail edge
        AddRock(v, i, new Vector3(318f, 0f, -48f), 1.5f);
        AddRock(v, i, new Vector3(348f, 0f,  40f), 1.3f);
        AddRock(v, i, new Vector3(375f, 0f, -30f), 1.0f);
        AddRock(v, i, new Vector3(405f, 0f,  38f), 1.8f);
        AddRock(v, i, new Vector3(415f, 0f,  -8f), 1.2f);

        // Hardy alpine trees (short, wind-battered)
        AddTree(v, i, new Vector3(310f, 0f, -44f), 1.8f);
        AddTree(v, i, new Vector3(330f, 0f,  46f), 2.0f);
        AddTree(v, i, new Vector3(360f, 0f, -50f), 1.6f);

        // Stone waymarker cairn at the turn
        MeshBuilder.AddBox(v, i,
            new Vector3(358f, 0f,    -4f), new Vector3(362f, 2.2f,  0f),
            new Color(105, 100, 92), new Color(70, 66, 60));
        MeshBuilder.AddBox(v, i,
            new Vector3(357f, 2.0f,  -5f), new Vector3(363f, 2.5f,  1f),
            new Color(115, 110, 102), new Color(78, 74, 68));
        MeshBuilder.AddBox(v, i,
            new Vector3(356.5f, 2.4f, -5.5f), new Vector3(363.5f, 2.75f, 1.5f),
            new Color(118, 112, 104), new Color(80, 76, 70));

        // Wind-carved rock arch (suggest alpine exposure)
        MeshBuilder.AddBox(v, i,
            new Vector3(380f, 0f,   -55f), new Vector3(388f, 5.5f, -48f),
            new Color(92, 88, 82), new Color(62, 59, 55));
        MeshBuilder.AddBox(v, i,
            new Vector3(392f, 0f,   -55f), new Vector3(400f, 5.5f, -48f),
            new Color(92, 88, 82), new Color(62, 59, 55));
        MeshBuilder.AddBox(v, i,
            new Vector3(380f, 4.8f, -55f), new Vector3(400f, 5.5f, -48f),
            new Color(98, 93, 87), new Color(65, 62, 57));

        AddStump(v, i, new Vector3(340f, 0f, -20f));
    }

    // Zone 10 – Plateau South (stone, X[-60,60] Z[300,420])
    private static void BuildPlateauSouth2(List<VertexPositionColor> v, List<int> i)
    {
        // Extended cliff walls (continuation of zone 7 walls)
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, 302f), new Vector3(-50f, 6.5f, 418f),
            new Color(72, 68, 65), new Color(48, 45, 42));
        MeshBuilder.AddBox(v, i,
            new Vector3( 50f, 0f, 302f), new Vector3( 60f, 6.5f, 418f),
            new Color(72, 68, 65), new Color(48, 45, 42));

        // Ancient collapsed wall section (empire ruins)
        MeshBuilder.AddBox(v, i,
            new Vector3(-45f, 0f, 338f), new Vector3( 45f, 2.4f, 344f),
            new Color(82, 78, 73), new Color(56, 52, 48));
        MeshBuilder.AddBox(v, i,    // gap in wall (collapsed section)
            new Vector3(-45f, 0f, 338f), new Vector3(-20f, 2.4f, 344f),
            new Color(82, 78, 73), new Color(56, 52, 48));
        AddRock(v, i, new Vector3( 15f, 0f, 341f), 1.8f);  // rubble in gap

        // Boulders along the pass
        AddRock(v, i, new Vector3(-32f, 0f, 316f), 1.6f);
        AddRock(v, i, new Vector3( 28f, 0f, 328f), 1.2f);
        AddRock(v, i, new Vector3(-18f, 0f, 362f), 1.4f);
        AddRock(v, i, new Vector3( 38f, 0f, 382f), 1.8f);
        AddRock(v, i, new Vector3(-40f, 0f, 405f), 1.5f);
        AddRock(v, i, new Vector3( 12f, 0f, 412f), 1.0f);

        // Heavy snow cover (more than zone 7)
        Color snowB = new Color(215, 222, 230);
        Color snowD = new Color(180, 188, 198);
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, 310f), new Vector3(-48f, 0.18f, 360f), snowB, snowD);
        MeshBuilder.AddBox(v, i,
            new Vector3( 46f, 0f, 320f), new Vector3( 60f, 0.18f, 380f), snowB, snowD);
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, 375f), new Vector3(-50f, 0.15f, 418f), snowB, snowD);
        MeshBuilder.AddBox(v, i,
            new Vector3( 48f, 0f, 370f), new Vector3( 60f, 0.15f, 418f), snowB, snowD);
        MeshBuilder.AddBox(v, i,
            new Vector3(-20f, 0f, 410f), new Vector3( 20f, 0.12f, 418f), snowB, snowD);

        // Stone distance markers
        MeshBuilder.AddBox(v, i,
            new Vector3(-4f, 0f, 325f), new Vector3(4f, 1.8f, 327f),
            new Color(100, 95, 88), new Color(68, 64, 58));
        MeshBuilder.AddBox(v, i,
            new Vector3(-4f, 0f, 395f), new Vector3(4f, 1.6f, 397f),
            new Color(100, 95, 88), new Color(68, 64, 58));

        // Frozen stream suggestion crossing the pass
        MeshBuilder.AddBox(v, i,
            new Vector3(-30f, 0.02f, 357f), new Vector3(30f, 0.06f, 362f),
            new Color(195, 210, 230), new Color(160, 176, 198));
    }

    // Zone 11 – Whispering Woods (grass, X[180,300] Z[60,180])
    private static void BuildWhisperingWoods(List<VertexPositionColor> v, List<int> i)
    {
        // Dense old-growth canopy — very tall trees in tight groups
        (float x, float z, float h)[] trees =
        {
            // North edge (entry from Nuvmito Trail)
            (188f,  68f, 4.2f), (198f,  72f, 3.8f), (210f,  66f, 4.5f),
            (226f,  70f, 4.0f), (244f,  68f, 4.3f), (260f,  72f, 3.9f),
            (278f,  66f, 4.6f), (292f,  70f, 4.1f),
            // West edge
            (184f,  90f, 4.4f), (186f, 110f, 4.0f), (184f, 132f, 4.5f),
            (188f, 154f, 3.9f), (186f, 170f, 4.2f),
            // East edge
            (294f,  88f, 4.3f), (296f, 108f, 4.6f), (292f, 130f, 4.0f),
            (296f, 152f, 4.4f), (292f, 170f, 4.1f),
            // Interior clusters (leave central clearing open ~x230-260, z110-145)
            (200f, 100f, 4.2f), (215f,  95f, 3.8f),
            (280f,  98f, 4.0f), (270f, 105f, 4.4f),
            (205f, 160f, 4.3f), (218f, 168f, 3.9f),
            (275f, 162f, 4.5f), (265f, 155f, 4.0f),
            // South edge
            (192f, 175f, 4.0f), (220f, 178f, 4.2f), (248f, 176f, 3.9f),
            (272f, 178f, 4.3f), (292f, 175f, 4.0f),
        };
        foreach (var (x, z, h) in trees)
            AddTree(v, i, new Vector3(x, 0f, z), h);

        // Mossy rocks (greenish tint, suggested by custom color)
        MeshBuilder.AddBox(v, i,
            new Vector3(228f, 0f, 102f), new Vector3(236f, 0.7f, 110f),
            new Color(88, 105, 78), new Color(60, 72, 52));
        MeshBuilder.AddBox(v, i,
            new Vector3(234f, 0.65f, 104f), new Vector3(242f, 0.98f, 108f),
            new Color(98, 116, 85), new Color(68, 80, 58));
        MeshBuilder.AddBox(v, i,
            new Vector3(258f, 0f,  152f), new Vector3(268f, 0.72f, 162f),
            new Color(85, 102, 75), new Color(58, 70, 50));
        MeshBuilder.AddBox(v, i,
            new Vector3(262f, 0.68f, 155f), new Vector3(270f, 1.0f, 160f),
            new Color(95, 112, 82), new Color(65, 78, 55));

        // Root ridges around the largest trees
        AddRoots(v, i, new Vector3(210f, 0f,  66f), 1.4f);
        AddRoots(v, i, new Vector3(278f, 0f,  66f), 1.5f);
        AddRoots(v, i, new Vector3(184f, 0f, 132f), 1.3f);
        AddRoots(v, i, new Vector3(296f, 0f, 130f), 1.4f);

        // Fallen logs in clearing
        MeshBuilder.AddBox(v, i,
            new Vector3(228f, 0f, 120f), new Vector3(255f, 0.55f, 126f),
            new Color(88, 60, 28), new Color(60, 40, 16));
        MeshBuilder.AddBox(v, i,
            new Vector3(238f, 0f, 138f), new Vector3(242f, 0.52f, 168f),
            new Color(84, 58, 26), new Color(58, 38, 14));

        // Stumps
        AddStump(v, i, new Vector3(222f, 0f,  90f));
        AddStump(v, i, new Vector3(270f, 0f, 170f));
        AddStump(v, i, new Vector3(246f, 0f, 108f));
    }

    // Cave mushroom — glowing cyan cap on a pale stem
    private static void AddMushroom(List<VertexPositionColor> v, List<int> i,
                                    Vector3 pos, float scale = 1f)
    {
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.10f * scale, 0f,           -0.10f * scale),
            pos + new Vector3( 0.10f * scale,  0.46f * scale, 0.10f * scale),
            new Color(172, 182, 162), new Color(122, 130, 114));
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-0.36f * scale,  0.38f * scale, -0.36f * scale),
            pos + new Vector3( 0.36f * scale,  0.60f * scale,  0.36f * scale),
            new Color(42, 198, 172), new Color(26, 138, 118));
    }

    // Surface root ridges radiating from a tree base
    private static void AddRoots(List<VertexPositionColor> v, List<int> i,
                                  Vector3 pos, float reach = 1.2f)
    {
        float[] angles = { 0f, MathF.PI * 0.5f, MathF.PI, MathF.PI * 1.5f };
        foreach (float a in angles)
        {
            float cx = MathF.Cos(a) * reach * 0.55f;
            float cz = MathF.Sin(a) * reach * 0.55f;
            MeshBuilder.AddBox(v, i,
                new Vector3(pos.X + cx - 0.09f, 0f,     pos.Z + cz - 0.09f),
                new Vector3(pos.X + cx + 0.09f, 0.22f,  pos.Z + cz + 0.09f),
                new Color(88, 55, 24), new Color(60, 38, 14));
        }
    }

    // Mine support frame — two uprights and a crossbeam spanning ~3.2 u wide
    private static void AddTimber(List<VertexPositionColor> v, List<int> i,
                                  Vector3 pos, float h = 2.8f)
    {
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-1.6f, 0f,    -0.15f),
            pos + new Vector3(-1.3f, h,      0.15f),
            new Color(102, 74, 42), new Color(70, 50, 26));
        MeshBuilder.AddBox(v, i,
            pos + new Vector3( 1.3f, 0f,    -0.15f),
            pos + new Vector3( 1.6f, h,      0.15f),
            new Color(102, 74, 42), new Color(70, 50, 26));
        MeshBuilder.AddBox(v, i,
            pos + new Vector3(-1.65f, h - 0.28f, -0.2f),
            pos + new Vector3( 1.65f, h,           0.2f),
            new Color(90, 64, 34), new Color(62, 44, 22));
    }

    private static void AddBench(List<VertexPositionColor> v, List<int> i,
                                 Vector3 pos, float yawRad)
    {
        // Seat
        float cos = MathF.Cos(yawRad), sin = MathF.Sin(yawRad);
        // Rotate offset (1.4, 0, 0) by yaw
        var offset = new Vector3(cos * 1.4f, 0f, sin * 1.4f);
        var centre = pos + offset;
        MeshBuilder.AddBox(v, i,
            centre + new Vector3(-1.1f, 0f,    -0.28f),
            centre + new Vector3( 1.1f, 0.48f,  0.28f),
            new Color(95, 72, 45), new Color(65, 48, 28));
        // Legs
        foreach (float lx in new[] { -0.85f, 0.85f })
            MeshBuilder.AddBox(v, i,
                centre + new Vector3(lx - 0.1f, 0f,    -0.22f),
                centre + new Vector3(lx + 0.1f, 0.46f,  0.22f),
                new Color(78, 58, 34), new Color(52, 38, 20));
    }

    // Zone 5 – Lumina Caves entrance (stone, X[-60,60] Z[-300,-180])
    private static void BuildCaveEntrance(List<VertexPositionColor> v, List<int> i)
    {
        // Mine mouth arch just past the portal (z ≈ -185)
        AddTimber(v, i, new Vector3(0f, 0f, -192f), 3.4f);

        // Two more support frames deeper in
        AddTimber(v, i, new Vector3(-8f,  0f, -228f), 3.0f);
        AddTimber(v, i, new Vector3( 6f,  0f, -264f), 3.0f);

        // Boulder clusters — left wall, right wall, centre
        AddRock(v, i, new Vector3(-42f, 0f, -198f), 1.6f);
        AddRock(v, i, new Vector3(-30f, 0f, -215f), 1.1f);
        AddRock(v, i, new Vector3(-48f, 0f, -248f), 1.8f);
        AddRock(v, i, new Vector3(-20f, 0f, -272f), 1.0f);
        AddRock(v, i, new Vector3( 38f, 0f, -205f), 1.4f);
        AddRock(v, i, new Vector3( 45f, 0f, -235f), 1.2f);
        AddRock(v, i, new Vector3( 30f, 0f, -260f), 1.5f);
        AddRock(v, i, new Vector3( 18f, 0f, -288f), 1.0f);
        AddRock(v, i, new Vector3(-12f, 0f, -290f), 0.9f);

        // Rubble pile at far north wall
        AddRock(v, i, new Vector3(-35f, 0f, -295f), 2.0f);
        AddRock(v, i, new Vector3( 35f, 0f, -295f), 1.8f);

        // Ore-seam hint — darker angular blocks on cave walls
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f,   -220f), new Vector3(-52f, 2.5f, -190f),
            new Color(80, 75, 68), new Color(52, 48, 42));
        MeshBuilder.AddBox(v, i,
            new Vector3( 52f, 0f,   -255f), new Vector3( 60f, 3.0f, -220f),
            new Color(80, 75, 68), new Color(52, 48, 42));
    }

    // Zone 6 – Nuvmito Trail (grass, X[180,300] Z[-60,60])
    private static void BuildNuvmitoTrail(List<VertexPositionColor> v, List<int> i)
    {
        // Dense tree canopy along north and south edges
        (float x, float z, float h)[] trees =
        {
            (192f, -48f, 3.0f), (212f, -52f, 2.8f), (234f, -46f, 3.2f),
            (258f, -50f, 2.9f), (278f, -54f, 3.1f), (295f, -44f, 2.7f),
            (192f,  46f, 3.1f), (215f,  50f, 2.6f), (238f,  48f, 3.3f),
            (260f,  52f, 2.8f), (280f,  46f, 3.0f), (293f,  50f, 2.9f),
            (200f, -28f, 2.5f), (288f, -24f, 2.6f),
            (198f,  30f, 2.7f), (290f,  26f, 2.5f),
        };
        foreach (var (x, z, h) in trees)
            AddTree(v, i, new Vector3(x, 0f, z), h);

        // Rocks along trail edges
        AddRock(v, i, new Vector3(210f, 0f, -18f), 1.1f);
        AddRock(v, i, new Vector3(248f, 0f,  22f), 1.3f);
        AddRock(v, i, new Vector3(270f, 0f, -30f), 0.9f);
        AddRock(v, i, new Vector3(285f, 0f,  10f), 1.2f);

        // Fallen log across trail
        MeshBuilder.AddBox(v, i,
            new Vector3(225f, 0f,   -14f), new Vector3(242f, 0.55f, 14f),
            new Color(94, 62, 30), new Color(64, 42, 18));

        // Old Myriac stone waymarker post
        MeshBuilder.AddBox(v, i,
            new Vector3(230f, 0f,   -0.4f), new Vector3(232f, 2.6f,  0.4f),
            new Color(105, 100, 92), new Color(70, 66, 60));
        MeshBuilder.AddBox(v, i,
            new Vector3(229.2f, 2.5f, -0.7f), new Vector3(232.8f, 2.85f, 0.7f),
            new Color(115, 110, 100), new Color(78, 74, 68));

        // Stumps
        AddStump(v, i, new Vector3(256f, 0f, -38f));
        AddStump(v, i, new Vector3(272f, 0f,  34f));
    }

    // Zone 7 – Plateau Pass (stone, X[-60,60] Z[180,300])
    private static void BuildPlateauPass(List<VertexPositionColor> v, List<int> i)
    {
        // Cliff-face walls flanking the pass (east and west)
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, 182f), new Vector3(-50f, 5.5f, 298f),
            new Color(78, 74, 70), new Color(52, 49, 46));
        MeshBuilder.AddBox(v, i,
            new Vector3( 50f, 0f, 182f), new Vector3( 60f, 5.5f, 298f),
            new Color(78, 74, 70), new Color(52, 49, 46));

        // Buttress blocks jutting from the cliff walls
        float[] buttressZ = { 200f, 225f, 252f, 278f };
        foreach (float bz in buttressZ)
        {
            MeshBuilder.AddBox(v, i,
                new Vector3(-50f, 0f, bz - 4f), new Vector3(-40f, 3.5f, bz + 4f),
                new Color(85, 80, 76), new Color(57, 54, 50));
            MeshBuilder.AddBox(v, i,
                new Vector3( 40f, 0f, bz - 4f), new Vector3( 50f, 3.5f, bz + 4f),
                new Color(85, 80, 76), new Color(57, 54, 50));
        }

        // Boulders and scree on the pass floor
        AddRock(v, i, new Vector3(-30f, 0f, 196f), 1.5f);
        AddRock(v, i, new Vector3( 24f, 0f, 208f), 1.2f);
        AddRock(v, i, new Vector3(-18f, 0f, 238f), 1.0f);
        AddRock(v, i, new Vector3( 35f, 0f, 258f), 1.4f);
        AddRock(v, i, new Vector3(-28f, 0f, 275f), 1.3f);
        AddRock(v, i, new Vector3( 12f, 0f, 290f), 1.6f);

        // Snow patches on boulder tops and against the cliff
        Color snowBright = new Color(210, 218, 225);
        Color snowDark   = new Color(175, 185, 195);
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, 210f), new Vector3(-48f, 0.15f, 240f),
            snowBright, snowDark);
        MeshBuilder.AddBox(v, i,
            new Vector3( 46f, 0f, 245f), new Vector3( 60f, 0.15f, 275f),
            snowBright, snowDark);
        MeshBuilder.AddBox(v, i,
            new Vector3(-60f, 0f, 268f), new Vector3(-50f, 0.12f, 290f),
            snowBright, snowDark);

        // Stone distance markers
        MeshBuilder.AddBox(v, i,
            new Vector3(-4f, 0f, 210f), new Vector3(4f, 1.8f, 212f),
            new Color(100, 95, 88), new Color(68, 64, 58));
        MeshBuilder.AddBox(v, i,
            new Vector3(-4f, 0f, 268f), new Vector3(4f, 1.8f, 270f),
            new Color(100, 95, 88), new Color(68, 64, 58));
    }

    private static void AddWell(List<VertexPositionColor> v, List<int> i, Vector3 pos)
    {
        // Stone ring wall — four corner posts
        const float r = 1.0f, wall = 0.25f, h = 0.9f;
        (float dx, float dz)[] corners = { (-r, -r), (r, -r), (r, r), (-r, r) };
        foreach (var (dx, dz) in corners)
        {
            MeshBuilder.AddBox(v, i,
                new Vector3(pos.X + dx - wall, pos.Y, pos.Z + dz - wall),
                new Vector3(pos.X + dx + wall, pos.Y + h, pos.Z + dz + wall),
                new Color(98, 93, 88), new Color(66, 62, 58));
        }
        // Top crossbar support post
        MeshBuilder.AddBox(v, i,
            new Vector3(pos.X - 0.08f, pos.Y + h, pos.Z - r - 0.15f),
            new Vector3(pos.X + 0.08f, pos.Y + h + 1.3f, pos.Z - r + 0.15f),
            new Color(85, 60, 30), new Color(58, 40, 18));
        MeshBuilder.AddBox(v, i,
            new Vector3(pos.X - 0.08f, pos.Y + h, pos.Z + r - 0.15f),
            new Vector3(pos.X + 0.08f, pos.Y + h + 1.3f, pos.Z + r + 0.15f),
            new Color(85, 60, 30), new Color(58, 40, 18));
        // Crossbeam
        MeshBuilder.AddBox(v, i,
            new Vector3(pos.X - 0.1f, pos.Y + h + 1.2f, pos.Z - r - 0.1f),
            new Vector3(pos.X + 0.1f, pos.Y + h + 1.4f, pos.Z + r + 0.1f),
            new Color(88, 62, 32), new Color(60, 42, 20));
    }
}
