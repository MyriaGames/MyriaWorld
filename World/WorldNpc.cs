using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaLib.Entities.NPCs;
using MyriaLib.Systems.Enums;

namespace MyriaWorld.World;

/// <summary>
/// A static NPC entity in the 3D world. Holds a pre-baked mesh and its
/// display name; interaction logic lives in WorldScreen.
/// </summary>
public sealed class WorldNpc
{
    public Npc     Data        { get; }
    public Vector3 Position    { get; }
    public string  DisplayName { get; }
    public string  Description { get; }

    public VertexPositionColor[] MeshVerts { get; }
    public int[]                 MeshIdx   { get; }

    public const float InteractRange = 4.5f;

    public WorldNpc(Npc npc, Vector3 position, string displayName, string description)
    {
        Data        = npc;
        Position    = position;
        DisplayName = displayName;
        Description = description;
        (MeshVerts, MeshIdx) = BuildMesh(npc.Type);
    }

    // ── Mesh ──────────────────────────────────────────────────────────────────

    private static (VertexPositionColor[], int[]) BuildMesh(NpcType type)
    {
        var (bright, dark, accent) = ColorsFor(type);
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();

        // Body (slightly taller than monsters to look more "human")
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.28f, 0f,    -0.18f),
            new Vector3( 0.28f, 1.55f,  0.18f),
            bright, dark);

        // Head
        Color headBright = MeshBuilder.Lerp(bright, Color.White, 0.35f);
        Color headDark   = MeshBuilder.Lerp(dark,   Color.Black, 0.1f);
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.18f, 1.55f, -0.18f),
            new Vector3( 0.18f, 1.9f,   0.18f),
            headBright, headDark);

        // Small accent stripe across chest (shows NPC role at a glance)
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.29f, 0.7f,  -0.19f),
            new Vector3( 0.29f, 0.9f,   0.19f),
            accent, MeshBuilder.Lerp(accent, Color.Black, 0.3f));

        return ([.. verts], [.. idx]);
    }

    private static (Color bright, Color dark, Color accent) ColorsFor(NpcType type) => type switch
    {
        NpcType.Healer      => (new Color(200, 215, 210), new Color(130, 145, 140),
                                new Color(50,  200, 160)),
        NpcType.Shop        => (new Color(205, 185, 150), new Color(140, 125, 100),
                                new Color(210, 170,  40)),
        NpcType.Smith       => (new Color(140, 120, 100), new Color(90,  75,  58),
                                new Color(80,  80,  90)),
        NpcType.SkillMaster => (new Color(190, 180, 210), new Color(125, 115, 145),
                                new Color(110,  70, 200)),
        NpcType.Villager    => (new Color(200, 175, 140), new Color(140, 120,  95),
                                new Color(160, 100,  50)),
        NpcType.Leathersmith=> (new Color(175, 135,  90), new Color(115,  85,  55),
                                new Color(120,  70,  30)),
        NpcType.Tailor      => (new Color(200, 175, 210), new Color(140, 120, 148),
                                new Color(180, 130, 210)),
        NpcType.Artificer   => (new Color(165, 210, 215), new Color(108, 145, 150),
                                new Color( 50, 200, 210)),
        NpcType.Enchanter   => (new Color(175, 155, 210), new Color(115, 100, 148),
                                new Color(160,  60, 210)),
        NpcType.Woodcutter  => (new Color(165, 130,  90), new Color(108,  85,  55),
                                new Color( 90, 130,  50)),
        NpcType.Miner       => (new Color(145, 130, 115), new Color( 95,  85,  72),
                                new Color( 80,  65,  55)),
        NpcType.Herbalist   => (new Color(170, 210, 165), new Color(112, 145, 108),
                                new Color( 60, 180,  70)),
        NpcType.Alchemist   => (new Color(210, 200, 155), new Color(148, 140, 105),
                                new Color(200, 180,  40)),
        NpcType.Cook        => (new Color(215, 195, 170), new Color(152, 135, 115),
                                new Color(210, 130,  70)),
        _                   => (new Color(180, 165, 150), new Color(120, 108,  95),
                                new Color(160, 140, 120)),
    };
}
