using MyriaLib.Systems.Enums;

namespace Myria.Mono.World;

/// <summary>Fixed-position NPC spawn point baked into a navmesh face.</summary>
public record NpcPlacement(string Id, float X, float Z);

/// <summary>Fixed-position gathering node baked into a navmesh face.</summary>
public record GatherNodePlacement(GatheringType Type, string Label, float X, float Z);

/// <summary>
/// One polygon face of the navigation mesh.  Vertices are stored as indices
/// into <see cref="NavMesh.Vertices"/>.  Portals (shared edges to adjacent
/// faces with a different room ID) are computed once at load time.
/// </summary>
public class NavFace
{
    public int[]          VertexIndices  { get; init; } = [];
    public int?           RoomId         { get; init; }
    public string         RoomName       { get; init; } = "";
    public string         Terrain        { get; init; } = "grass";
    public NpcPlacement[]      NpcPlacements  { get; init; } = [];
    public GatherNodePlacement[] GatherNodes  { get; init; } = [];

    /// <summary>
    /// Each entry is a portal to a neighbouring face:
    /// (neighbour face index, shared vertex A index, shared vertex B index).
    /// </summary>
    public List<(int Face, int VA, int VB)> Portals { get; } = new();
}
