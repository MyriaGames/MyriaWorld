using Microsoft.Xna.Framework;
using Myria.Lib.Core.Entities.Maps;

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

    public static List<WorldMonster> Spawn(NavMesh navMesh, int faceIndex, Room room,
        IReadOnlyList<MonsterSpawnZones.Zone> zones)
    {
        var result = new List<WorldMonster>();
        if (room.Monsters.Count == 0) return result;

        int count = _rng.Next(MinSpawn, MaxSpawn + 1);
        for (int i = 0; i < count; i++)
        {
            var template = PickTemplate(room);
            if (template == null) continue;

            var xz  = zones.Count > 0
                ? RandomPointInZone(navMesh, faceIndex, zones[_rng.Next(zones.Count)])
                : RandomPointInFace(navMesh, faceIndex);
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
    private static Myria.Lib.Core.Entities.Monsters.Monster? PickTemplate(Room room)
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
    /// Returns a random XZ position inside the given spawn clearing, using rejection
    /// sampling within the zone's circle. Falls back to the zone center after 20
    /// failed attempts (e.g. a zone that clips a concave edge of the face).
    /// </summary>
    private static Vector2 RandomPointInZone(NavMesh navMesh, int faceIndex, MonsterSpawnZones.Zone zone)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float ang = (float)(_rng.NextDouble() * MathHelper.TwoPi);
            float r   = zone.Radius * MathF.Sqrt((float)_rng.NextDouble());
            var   p   = zone.Center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
            if (navMesh.FindFaceIndex(p) == faceIndex) return p;
        }
        return zone.Center;
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
