using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaLib.Entities.NPCs;
using MyriaLib.Systems.Enums;
using MyriaWorld.Services;

namespace MyriaWorld.World;

/// <summary>
/// A static NPC entity in the 3D world. Holds a pre-baked mesh and its
/// display name; interaction logic lives in WorldScreen.
/// </summary>
public sealed class WorldNpc
{
    public Vector3               Position      { get; set; }
    public string                Name          { get; }
    public string[]              DialogueLines { get; }
    public VertexPositionColor[] MeshVerts     { get; }
    public int[]                 MeshIdx       { get; }
    /// <summary>Underlying MyriaLib NPC, used for quest and shop lookups.</summary>
    public Npc                   Source        { get; }

    public const float InteractRange = 2.5f;

    // Backward-compat aliases used by WorldScreen
    public string DisplayName => Name;
    public Npc    Data        => Source;
    public string Description => string.IsNullOrEmpty(Source?.DescriptionKey)
        ? "" : WorldDataService.Localize(Source.DescriptionKey);

    private WorldNpc(Vector3 pos, string name, string[] dialogue, Npc source)
    {
        Position      = pos;
        Name          = name;
        DialogueLines = dialogue;
        Source        = source;
        (MeshVerts, MeshIdx) = BuildMesh();
    }

    private static (VertexPositionColor[], int[]) BuildMesh()
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();
        // Body (green robe — distinct from blue player and red/dark monsters)
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.28f, 0f,   -0.18f),
            new Vector3( 0.28f, 1.2f,  0.18f),
            new Color(70, 130, 70), new Color(50,  95, 50));
        // Head
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.18f, 1.2f,  -0.18f),
            new Vector3( 0.18f, 1.55f,  0.18f),
            new Color(220, 175, 135), new Color(190, 145, 110));
        return ([.. verts], [.. idx]);
    }

    /// <summary>
    /// Creates WorldNpc entities for every navmesh room that has NpcRefs populated.
    /// NPCs that have a matching WorldBuilding are placed inside that building's interior face.
    /// </summary>
    public static List<WorldNpc> SpawnAll(NavMesh mesh, IReadOnlyList<WorldBuilding> buildings)
    {
        var result = new List<WorldNpc>();

        for (int fi = 0; fi < mesh.Faces.Length; fi++)
        {
            var face = mesh.Faces[fi];
            if (!face.RoomId.HasValue) continue;

            var room = WorldDataService.GetRoom(face.RoomId.Value);
            if (room == null || room.NpcRefs.Count == 0) continue;

            Vector3  centroid = FaceCentroid(mesh, fi);
            Vector3[] offsets = SpacingOffsets(room.NpcRefs.Count);

            for (int ni = 0; ni < room.NpcRefs.Count; ni++)
            {
                var    npc      = room.NpcRefs[ni];
                string name     = SafeName(npc);
                var    dialogue = BuildDialogue(npc, face.RoomName ?? "");

                // NPCs with a building stand at the building center (player walks in)
                var    bldg = buildings.FirstOrDefault(b => b.NpcId == npc.Id);
                Vector3 pos = bldg != null
                    ? new Vector3(bldg.CenterX, 0f, bldg.CenterZ)
                    : centroid + offsets[ni];

                result.Add(new WorldNpc(pos, name, dialogue, npc));
            }
        }

        return result;
    }

    private static string SafeName(Npc npc)
    {
        try
        {
            string t = npc.ToString();
            string name = string.IsNullOrWhiteSpace(t) || t == npc.NameKey ? npc.Id : t;
            return WorldDataService.AsciiSafe(name);
        }
        catch { return npc.Id; }
    }

    private static string[] BuildDialogue(Npc npc, string roomName)
    {
        string safeRoom = WorldDataService.AsciiSafe(roomName);
        return npc.Type switch
        {
            NpcType.Healer      => ["I can mend your wounds and restore your mana.", "Rest well, traveler."],
            NpcType.Shop        => ["Fine wares for fine adventurers.", "I have what you need - for a price."],
            NpcType.Smith       => ["Bring me ore and I will forge you something worthy.", "Good steel outlasts its maker."],
            NpcType.SkillMaster => ["Train hard and the skills will follow.", "Show me your dedication."],
            NpcType.Alchemist   => ["The right potion at the right moment saves lives.", "My brews are unmatched."],
            NpcType.Herbalist   => ["Nature provides, if you know where to look.", "I trade in roots and remedies."],
            _                   => [$"Welcome to {safeRoom}.", "Safe travels to you, friend."],
        };
    }

    // Spread multiple NPCs around the face centroid
    private static Vector3[] SpacingOffsets(int count) => count switch
    {
        1 => [new Vector3(0f, 0f, 2f)],
        2 => [new Vector3(-2f, 0f, 2f), new Vector3(2f, 0f, 2f)],
        3 => [new Vector3(0f, 0f, 2f), new Vector3(-2.5f, 0f, 3f), new Vector3(2.5f, 0f, 3f)],
        _ => Enumerable.Range(0, count)
              .Select(i => new Vector3(
                  MathF.Cos(MathF.Tau * i / count) * 3f, 0f,
                  MathF.Sin(MathF.Tau * i / count) * 3f))
              .ToArray(),
    };

    private static Vector3 FaceCentroid(NavMesh mesh, int fi)
    {
        var   face = mesh.Faces[fi];
        float sumX = 0f, sumZ = 0f;
        foreach (int vi in face.VertexIndices)
        {
            sumX += mesh.Vertices[vi].X;
            sumZ += mesh.Vertices[vi].Y;
        }
        int n = face.VertexIndices.Length;
        return new Vector3(sumX / n, 0f, sumZ / n);
    }
}
