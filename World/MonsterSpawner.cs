using Microsoft.Xna.Framework;
using MyriaLib.Entities.Maps;

namespace Myria.Mono.World;

/// <summary>
/// Creates <see cref="WorldMonster"/> instances for a navmesh face whose room
/// has <see cref="Room.HasMonsters"/> == true.
/// </summary>
public static class MonsterSpawner
{
    private static readonly Random _rng = new();

    // How many monsters to spawn per face entry (inclusive)
    private const int MinSpawn = 2;
    private const int MaxSpawn = 4;

    public static List<WorldMonster> Spawn(NavMesh navMesh, int faceIndex, Room room)
    {
        var result = new List<WorldMonster>();
        if (room.Monsters.Count == 0) return result;

        int count = _rng.Next(MinSpawn, MaxSpawn + 1);
        for (int i = 0; i < count; i++)
        {
            var template = PickTemplate(room);
            if (template == null) continue;

            var xz  = RandomPointInFace(navMesh, faceIndex);
            var pos = new Vector3(xz.X, 0f, xz.Y);
            result.Add(new WorldMonster(template, pos, faceIndex));
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Weighted-random selection from the room's encounter table.
    /// Returns null if the room has no monster templates wired in.
    /// </summary>
    private static MyriaLib.Entities.Monsters.Monster? PickTemplate(Room room)
    {
        if (room.Monsters.Count == 0) return null;

        float total = room.EncounterableMonsters.Values.Sum();
        if (total <= 0f)
            return room.Monsters[_rng.Next(room.Monsters.Count)];

        float roll = (float)_rng.NextDouble() * total;
        float cum  = 0f;
        foreach (var (monsterId, weight) in room.EncounterableMonsters)
        {
            cum += weight;
            if (roll <= cum)
            {
                var template = room.Monsters.FirstOrDefault(m => m.Id == monsterId);
                if (template != null) return template;
            }
        }
        return room.Monsters[_rng.Next(room.Monsters.Count)];
    }

    /// <summary>
    /// Returns a random XZ position inside the given navmesh face, using
    /// rejection sampling within the face's bounding box.
    /// Falls back to the face centroid after 20 failed attempts.
    /// </summary>
    private static Vector2 RandomPointInFace(NavMesh navMesh, int faceIndex)
    {
        var face = navMesh.Faces[faceIndex];

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (int vi in face.VertexIndices)
        {
            var v = navMesh.Vertices[vi];
            if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
            if (v.Y < minZ) minZ = v.Y;  if (v.Y > maxZ) maxZ = v.Y;
        }

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = minX + (float)_rng.NextDouble() * (maxX - minX);
            float z = minZ + (float)_rng.NextDouble() * (maxZ - minZ);
            var   p = new Vector2(x, z);
            if (navMesh.FindFaceIndex(p) == faceIndex) return p;
        }

        // Centroid fallback
        float cx = face.VertexIndices.Average(vi => navMesh.Vertices[vi].X);
        float cz = face.VertexIndices.Average(vi => navMesh.Vertices[vi].Y);
        return new Vector2(cx, cz);
    }
}
