namespace MyriaWorld.World;

/// <summary>
/// One polygon face of the navigation mesh.  Vertices are stored as indices
/// into <see cref="NavMesh.Vertices"/>.  Portals (shared edges to adjacent
/// faces with a different room ID) are computed once at load time.
/// </summary>
public class NavFace
{
    public int[]   VertexIndices { get; init; } = [];
    public int?    RoomId        { get; init; }
    public string  RoomName      { get; init; } = "";
    public string  Terrain       { get; init; } = "grass";

    /// <summary>
    /// Each entry is a portal to a neighbouring face:
    /// (neighbour face index, shared vertex A index, shared vertex B index).
    /// </summary>
    public List<(int Face, int VA, int VB)> Portals { get; } = new();
}
