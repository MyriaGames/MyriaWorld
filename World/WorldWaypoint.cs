using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Myria.Mono.World;

public sealed class WorldWaypoint
{
    public string  Name     { get; }
    public int     RoomId   { get; }
    public Vector3 Position { get; }

    public const float InteractRange = 5f;

    public VertexPositionColor[] MeshVerts { get; }
    public int[]                 MeshIdx   { get; }

    public WorldWaypoint(string name, int roomId, Vector3 position)
    {
        Name     = name;
        RoomId   = roomId;
        Position = position;
        (MeshVerts, MeshIdx) = BuildMesh();
    }

    // Stone pillar with glowing blue crystal cap
    private static (VertexPositionColor[], int[]) BuildMesh()
    {
        var v = new List<VertexPositionColor>();
        var i = new List<int>();

        var stoneB = new Color(130, 122, 138);
        var stoneD = new Color( 88,  82,  95);
        var crystB = new Color(140, 210, 255);
        var crystD = new Color( 70, 140, 220);

        // Base plinth (wide, short)
        MeshBuilder.AddBox(v, i,
            new Vector3(-0.7f, 0f,   -0.7f),
            new Vector3( 0.7f, 0.4f,  0.7f),
            stoneB, stoneD);

        // Pillar shaft
        MeshBuilder.AddBox(v, i,
            new Vector3(-0.3f, 0.4f, -0.3f),
            new Vector3( 0.3f, 2.6f,  0.3f),
            stoneB, stoneD);

        // Crystal upward spike
        MeshBuilder.AddPyramid(v, i,
            new Vector3(-0.45f, 2.6f, -0.45f),
            new Vector3( 0.45f, 2.6f,  0.45f),
            3.8f, crystB, crystD);

        return ([.. v], [.. i]);
    }
}
