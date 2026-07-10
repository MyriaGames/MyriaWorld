using System.Text.Json;
using Microsoft.Xna.Framework;

namespace MyriaWorld.World;

public static class NavMeshLoader
{
    // JSON DTOs
    private sealed record MeshDto(float[][] Vertices, FaceDto[] Faces);
    private sealed record FaceDto(int[] V, int? RoomId, string? RoomName, string? Terrain,
                                  NpcPlacementDto[]? Npcs, GatherNodeDto[]? GatherNodes);
    private sealed record NpcPlacementDto(string Id, float X, float Z);
    private sealed record GatherNodeDto(string Type, string Label, float X, float Z);

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static NavMesh Load(string path)
    {
        var dto = JsonSerializer.Deserialize<MeshDto>(File.ReadAllText(path), _opts)
            ?? throw new InvalidDataException($"Failed to parse navmesh: {path}");

        var vertices = dto.Vertices
            .Select(v => new Vector2(v[0], v[1]))
            .ToArray();

        var faces = dto.Faces.Select(f => new NavFace
        {
            VertexIndices = f.V,
            RoomId        = f.RoomId,
            RoomName      = f.RoomName ?? "",
            Terrain       = f.Terrain  ?? "grass",
            NpcPlacements = f.Npcs?.Select(n => new NpcPlacement(n.Id, n.X, n.Z)).ToArray()
                            ?? [],
            GatherNodes   = f.GatherNodes?.Select(g => new GatherNodePlacement(
                                Enum.Parse<MyriaLib.Systems.Enums.GatheringType>(g.Type, ignoreCase: true),
                                g.Label, g.X, g.Z)).ToArray()
                            ?? [],
        }).ToArray();

        ComputePortals(faces);

        return new NavMesh(vertices, faces);
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// For every pair of faces that share an edge (two adjacent vertex indices),
    /// register a bidirectional portal entry.
    /// </summary>
    private static void ComputePortals(NavFace[] faces)
    {
        for (int i = 0; i < faces.Length; i++)
        {
            for (int j = i + 1; j < faces.Length; j++)
            {
                if (TrySharedEdge(faces[i].VertexIndices, faces[j].VertexIndices,
                                  out int va, out int vb))
                {
                    faces[i].Portals.Add((j, va, vb));
                    faces[j].Portals.Add((i, va, vb));
                }
            }
        }
    }

    private static bool TrySharedEdge(int[] a, int[] b, out int va, out int vb)
    {
        var setB = new HashSet<int>(b);
        va = vb = -1;
        for (int i = 0; i < a.Length; i++)
        {
            int v0 = a[i], v1 = a[(i + 1) % a.Length];
            if (setB.Contains(v0) && setB.Contains(v1))
            {
                va = v0; vb = v1;
                return true;
            }
        }
        return false;
    }
}
