using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myria.Lib.Core.Entities.Maps;
using Myria.Lib.Core.Systems.Enums;

namespace Myria.Mono.World;

/// <summary>
/// A static gathering node in the 3D world (ore vein, tree, herb patch).
/// Each node tracks its own daily gather limit independently of the room pool.
/// </summary>
public sealed class WorldGatherNode
{
    public string         Type             { get; }
    public Vector3        Position         { get; }
    public string         Label            { get; }
    public GatheringSpot? Spot             { get; }
    public int            GathersRemaining { get; private set; }
    public bool           IsDepleted       => GathersRemaining <= 0;

    public VertexPositionColorTexture[] MeshVerts { get; }
    public int[]                 MeshIdx   { get; }

    public const float InteractRange = 3.5f;

    public WorldGatherNode(string type, Vector3 position, string label, GatheringSpot? spot)
    {
        Type     = type;
        Position = position;
        Label    = label;
        Spot     = spot;
        GathersRemaining = spot != null ? Random.Shared.Next(1, 6) : 0;
        (MeshVerts, MeshIdx) = BuildMesh(type);
    }

    /// <summary>Consumes one gather charge. Returns false if already depleted.</summary>
    public bool TryConsume()
    {
        if (IsDepleted) return false;
        GathersRemaining--;
        return true;
    }

    private static (VertexPositionColorTexture[], int[]) BuildMesh(string type)
    {
        var verts = new List<VertexPositionColorTexture>();
        var idx   = new List<int>();

        switch (type)
        {
            case GatheringType.Ore:
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.55f, 0f,    -0.45f),
                    new Vector3( 0.55f, 0.65f,  0.45f),
                    new Color(130, 120, 108), new Color(80, 72, 62));
                MeshBuilder.AddBox(verts, idx,
                    new Vector3( 0.3f,  0f,     0.2f),
                    new Vector3( 0.75f, 0.42f,  0.6f),
                    new Color(115, 108, 95), new Color(70, 65, 55));
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.2f,  0.38f, -0.2f),
                    new Vector3( 0.2f,  0.66f,  0.1f),
                    new Color(170, 155, 80), new Color(110, 100, 50));
                break;

            case GatheringType.Tree:
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.22f, 0f,    -0.22f),
                    new Vector3( 0.22f, 1.6f,   0.22f),
                    new Color(110, 80, 50), new Color(70, 50, 30));
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.8f,  1.3f,  -0.8f),
                    new Vector3( 0.8f,  2.4f,   0.8f),
                    new Color(60, 140, 60), new Color(40, 100, 40));
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.5f,  2.2f,  -0.5f),
                    new Vector3( 0.5f,  2.9f,   0.5f),
                    new Color(75, 160, 65), new Color(45, 110, 40));
                break;

            case GatheringType.Herb:
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.4f,  0f,     -0.3f),
                    new Vector3( 0.1f,  0.38f,   0.15f),
                    new Color(80, 175, 80), new Color(50, 120, 50));
                MeshBuilder.AddBox(verts, idx,
                    new Vector3( 0.05f, 0f,     -0.15f),
                    new Vector3( 0.4f,  0.32f,   0.3f),
                    new Color(95, 185, 70), new Color(60, 130, 45));
                MeshBuilder.AddBox(verts, idx,
                    new Vector3(-0.25f, 0f,      0.1f),
                    new Vector3( 0.2f,  0.28f,   0.4f),
                    new Color(70, 160, 90), new Color(45, 110, 60));
                break;
        }

        return ([.. verts], [.. idx]);
    }
}
